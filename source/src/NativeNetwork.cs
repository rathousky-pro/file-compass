using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
namespace FileCompass.NetworkTransport
{
    internal static class NativeNetwork
    {
        const string Dll = "FileCompassNetwork.dll";
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern IntPtr LoadLibraryEx(string path, IntPtr file, uint flags);
        static NativeNetwork()
        {
            string path = Path.Combine(AppPaths.Resource("plugins"), "network", Dll);
            using (PluginIntegrity.OpenVerified(@"network\" + Dll))
            {
                if (LoadLibraryEx(path, IntPtr.Zero, 8) == IntPtr.Zero) throw new IOException(L.Text("network.pluginLoadFailedPrefix") + path + " (" + Marshal.GetLastWin32Error() + ")");
            }
            if (curl_global_init(3) != 0 || libssh2_init(0) != 0) throw new IOException(L.Text("network.pluginInitializationFailed"));
        }
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern IntPtr libssh2_agent_init(IntPtr session);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern int libssh2_agent_connect(IntPtr agent);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern int libssh2_agent_list_identities(IntPtr agent);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern int libssh2_agent_get_identity(IntPtr agent, out IntPtr identity, IntPtr previous);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern int libssh2_agent_userauth(IntPtr agent, byte[] username, IntPtr identity);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern int libssh2_agent_disconnect(IntPtr agent);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern void libssh2_agent_free(IntPtr agent);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern void libssh2_keepalive_config(IntPtr session, int reply, uint interval);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern int libssh2_keepalive_send(IntPtr session, out int seconds);
        internal static void Ensure() { }
        internal static byte[] Bytes(string text) { return Encoding.UTF8.GetBytes((text ?? "") + "\0"); }
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate UIntPtr Io(IntPtr data, UIntPtr size, UIntPtr count, IntPtr user);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int Progress(IntPtr user, long total, long done, long uploadTotal, long uploadDone);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int HostKey(IntPtr user, int type, IntPtr key, UIntPtr length);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void Keyboard(IntPtr name, int nameLength, IntPtr instruction, int instructionLength, int count, IntPtr prompts, IntPtr responses, IntPtr user);
        [StructLayout(LayoutKind.Sequential)] internal struct Attributes { public uint Flags; public ulong Size; public uint Uid, Gid, Permissions, Atime, Mtime; }
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int curl_global_init(int flags);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern IntPtr curl_easy_init();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern void curl_easy_cleanup(IntPtr easy);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern void curl_easy_reset(IntPtr easy);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern IntPtr curl_easy_strerror(int error);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern int cn_setopt_long(IntPtr easy, int option, int value);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern int cn_setopt_off(IntPtr easy, int option, long value);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern int cn_setopt_ptr(IntPtr easy, int option, IntPtr value);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern IntPtr curl_slist_append(IntPtr list, byte[] text);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern void curl_slist_free_all(IntPtr list);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern IntPtr curl_multi_init();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern int curl_multi_add_handle(IntPtr multi, IntPtr easy);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern int curl_multi_remove_handle(IntPtr multi, IntPtr easy);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern int curl_multi_perform(IntPtr multi, out int running);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern int curl_multi_poll(IntPtr multi, IntPtr fds, uint count, int timeout, out int ready);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern int curl_multi_wakeup(IntPtr multi);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern IntPtr curl_multi_info_read(IntPtr multi, out int left);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern int curl_multi_cleanup(IntPtr multi);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int libssh2_init(int flags);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern IntPtr cn_alloc(UIntPtr size);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern IntPtr libssh2_session_init_ex(IntPtr alloc, IntPtr free, IntPtr realloc, IntPtr user);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern int libssh2_session_flag(IntPtr session, int flag, int value);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern void libssh2_session_set_blocking(IntPtr session, int blocking);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern int libssh2_session_handshake(IntPtr session, IntPtr socket);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern int libssh2_session_method_pref(IntPtr session, int method, byte[] preference);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern IntPtr libssh2_session_hostkey(IntPtr session, out UIntPtr length, out int type);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern int libssh2_session_last_errno(IntPtr session);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern int libssh2_session_last_error(IntPtr session, out IntPtr text, out int length, int allocate);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern int libssh2_session_free(IntPtr session);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern int libssh2_userauth_password_ex(IntPtr session, byte[] name, uint nameLength, byte[] password, uint length, IntPtr change);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern int libssh2_userauth_publickey_frommemory(IntPtr session, byte[] name, UIntPtr nameLength, IntPtr publicKey, UIntPtr publicLength, byte[] privateKey, UIntPtr privateLength, IntPtr passphrase);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern int libssh2_userauth_keyboard_interactive_ex(IntPtr session, byte[] name, uint length, Keyboard callback);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern IntPtr libssh2_channel_open_ex(IntPtr session, byte[] type, uint typeLength, uint window, uint packet, IntPtr message, uint length);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern int libssh2_channel_process_startup(IntPtr channel, byte[] request, uint requestLength, byte[] message, uint length);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern IntPtr libssh2_channel_read_ex(IntPtr channel, int stream, byte[] buffer, UIntPtr length);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern int libssh2_channel_eof(IntPtr channel);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern int libssh2_channel_get_exit_status(IntPtr channel);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern int libssh2_channel_wait_closed(IntPtr channel);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern int libssh2_channel_free(IntPtr channel);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern IntPtr libssh2_channel_write_ex(IntPtr channel, int stream, IntPtr data, UIntPtr length);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern int libssh2_channel_send_eof(IntPtr channel);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern int libssh2_channel_wait_eof(IntPtr channel);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern IntPtr libssh2_scp_send64(IntPtr session, byte[] path, int mode, long size, long mtime, long atime);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern IntPtr cn_scp_recv(IntPtr session, byte[] path, out long size);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern IntPtr libssh2_sftp_init(IntPtr session);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern int libssh2_sftp_shutdown(IntPtr sftp);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern uint libssh2_sftp_last_error(IntPtr sftp);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern IntPtr libssh2_sftp_open_ex(IntPtr sftp, byte[] path, uint length, uint flags, int mode, int type);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern int libssh2_sftp_readdir_ex(IntPtr handle, byte[] name, UIntPtr length, IntPtr longEntry, UIntPtr longLength, out Attributes attrs);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern int libssh2_sftp_close_handle(IntPtr handle);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern IntPtr libssh2_sftp_read(IntPtr handle, byte[] buffer, UIntPtr length);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern IntPtr libssh2_sftp_write(IntPtr handle, IntPtr data, UIntPtr length);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern void libssh2_sftp_seek64(IntPtr handle, ulong offset);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern int libssh2_sftp_stat_ex(IntPtr sftp, byte[] path, uint length, int type, ref Attributes attrs);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern int libssh2_sftp_rename_ex(IntPtr sftp, byte[] source, uint sourceLength, byte[] target, uint targetLength, int flags);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern int libssh2_sftp_mkdir_ex(IntPtr sftp, byte[] path, uint length, int mode);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern int libssh2_sftp_rmdir_ex(IntPtr sftp, byte[] path, uint length);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] internal static extern int libssh2_sftp_unlink_ex(IntPtr sftp, byte[] path, uint length);
    }
}

