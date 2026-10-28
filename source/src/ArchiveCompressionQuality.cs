using System;
namespace FileCompass {
    internal static class ArchiveCompressionQuality {
        internal static void Validate(int level){if(level<0||level>9)throw new ArgumentOutOfRangeException("level");}
        internal static SharpSevenZip.CompressionLevel Native(int level){
            Validate(level);
            return level==0?SharpSevenZip.CompressionLevel.None:level<=1?SharpSevenZip.CompressionLevel.Fast:level<=3?SharpSevenZip.CompressionLevel.Low:level<=5?SharpSevenZip.CompressionLevel.Normal:level<=7?SharpSevenZip.CompressionLevel.High:SharpSevenZip.CompressionLevel.Ultra;
        }
        internal static string Rar(int level){Validate(level);return "-m"+Math.Min(5,(level*5+4)/9);}
        // ARJ uses the opposite order: m1 best, m4 fastest; m0 stores without compression.
        internal static string Arj(int level){Validate(level);return "-m"+(level==0?0:level<=2?4:level<=4?3:level<=7?2:1);}
    }
}
