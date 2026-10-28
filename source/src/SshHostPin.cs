using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
namespace FileCompass {
    internal static class SshHostPin {
        internal static string Algorithm(string value){byte[] digest;string algorithm;Parse(value,out algorithm,out digest);return algorithm;}
        internal static string Normalize(string value){byte[] digest;string algorithm;Parse(value,out algorithm,out digest);return algorithm+" SHA256:"+Convert.ToBase64String(digest).TrimEnd('=');}
        static void Parse(string value,out string algorithm,out byte[] digest){
            if(value==null||value.Length>16384)throw new IOException(L.Text("ssh.sha256Required"));
            var parts=value.Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries);algorithm=parts.Length==2?parts[0]:"";
            if(!new[]{"ssh-ed25519","ssh-rsa","ecdsa-sha2-nistp256","ecdsa-sha2-nistp384","ecdsa-sha2-nistp521","sk-ssh-ed25519@openssh.com","sk-ecdsa-sha2-nistp256@openssh.com"}.Contains(algorithm))throw new IOException(L.Text("ssh.sha256AlgorithmRequired"));
            string encoded=parts[1];if(encoded.StartsWith("MD5:",StringComparison.OrdinalIgnoreCase))throw new IOException(L.Text("ssh.md5Rejected"));
            bool hash=encoded.StartsWith("SHA256:",StringComparison.Ordinal);if(hash)encoded=encoded.Substring(7);
            try{digest=Convert.FromBase64String(encoded.PadRight((encoded.Length+3)/4*4,'='));}catch(FormatException ex){throw new IOException(L.Text("ssh.invalidHostKey"),ex);}
            if(digest.Length==32)return;
            if(hash||digest.Length<4)throw new IOException(L.Text("ssh.invalidFingerprintLength"));
            int length=(digest[0]<<24)|(digest[1]<<16)|(digest[2]<<8)|digest[3];if(length!=algorithm.Length||length>digest.Length-4||Encoding.ASCII.GetString(digest,4,length)!=algorithm)throw new IOException(L.Text("ssh.invalidAlgorithm"));
            using(var sha=SHA256.Create())digest=sha.ComputeHash(digest);
        }
        internal static bool Trusted(string expected,byte[] key){try{byte[] digest;string algorithm;Parse(expected,out algorithm,out digest);if(key.Length<4)return false;int n=(key[0]<<24)|(key[1]<<16)|(key[2]<<8)|key[3];if(n<1||n>key.Length-4||Encoding.ASCII.GetString(key,4,n)!=algorithm)return false;using(var sha=SHA256.Create()){var actual=sha.ComputeHash(key);int diff=0;for(int i=0;i<actual.Length;i++)diff|=actual[i]^digest[i];return diff==0;}}catch(IOException){return false;}}
    }
}
