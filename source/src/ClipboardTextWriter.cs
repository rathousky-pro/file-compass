using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
namespace FileCompass {
    internal static class ClipboardTextWriter {
        static readonly int[] Delays={40,80,120,200,350,550,800};
        static bool Busy(ExternalException error) {
            int code=error.ErrorCode;
            // WinForms can wrap an OLE clipboard failure as E_FAIL.
            return code==unchecked((int)0x800401D0)||code==unchecked((int)0x800401D1)||code==unchecked((int)0x800401D2)||code==unchecked((int)0x80004005);
        }
        static void Write(string text) {
            var data=new System.Windows.Forms.DataObject();
            data.SetData(System.Windows.Forms.DataFormats.UnicodeText,true,text);
            System.Windows.Forms.Clipboard.SetDataObject(data,true,1,0);
        }
        internal static async Task<bool> TryWriteAsync(string text,CancellationToken token=default(CancellationToken),Action<string> writer=null,Func<int,Task> pause=null) {
            if(text==null)throw new ArgumentNullException("text");
            writer=writer??Write;pause=pause??(milliseconds=>Task.Delay(milliseconds,token));
            for(int attempt=0;attempt<=Delays.Length;attempt++) {
                token.ThrowIfCancellationRequested();
                try{writer(text);return true;}
                catch(ExternalException ex){if(!Busy(ex))throw;if(attempt==Delays.Length)return false;}
                await pause(Delays[attempt]);
            }
            return false;
        }
    }
}

