"""Generate small, synthetic ext2 and partition-table test fixtures; no user disks are opened."""
import struct, gzip, uuid, zlib, pathlib
out=pathlib.Path(__file__).resolve().parent
image=bytearray(1024*1024)
def put(buf,at,fmt,*v):struct.pack_into('<'+fmt,buf,at,*v)
sb=1024
for offset,value in [(0,128),(4,1024),(12,980),(16,100),(20,1),(24,0),(28,0),(32,8192),(36,8192),(40,128),(76,1),(84,11),(92,0),(96,2),(100,3)]:put(image,sb+offset,'I',value)
put(image,sb+56,'H',0xef53);put(image,sb+58,'H',1);put(image,sb+60,'H',1);put(image,sb+88,'H',128)
image[sb+104:sb+120]=bytes(range(16));image[sb+120:sb+136]=b'FileCompassDemo\0'
put(image,2048,'III',3,4,5)
def inode(n,mode,size,blocks,ptr=0):
 at=5*1024+(n-1)*128
 put(image,at,'H',mode);put(image,at+4,'I',size&0xffffffff)
 for o in (8,12,16):put(image,at+o,'I',1700000000)
 put(image,at+26,'H',1);put(image,at+28,'I',blocks);put(image,at+40,'I',ptr)
 if mode&0xf000==0x8000:put(image,at+108,'I',size>>32)
 return at
def directory(block,items):
 pos=block*1024
 for idx,(n,name,kind) in enumerate(items):
  raw=name.encode();size=(8+len(raw)+3)&~3
  if idx==len(items)-1:size=block*1024+1024-pos
  put(image,pos,'IHBB',n,size,len(raw),kind);image[pos+8:pos+8+len(raw)]=raw;pos+=size
inode(2,0x41ed,1024,2,21);inode(12,0x41ed,1024,2,22)
for n,block,text in [(13,23,b'hello from ext2\n'),(14,24,'Příliš žluťoučký kůň\n'.encode()),(17,25,b'UPPER\n'),(18,26,b'lower\n')]:inode(n,0x81a4,len(text),2,block);image[block*1024:block*1024+len(text)]=text
inode(15,0x21a4,0,0);at=inode(16,0xa1ff,9,0);image[at+40:at+49]=b'hello.txt'
inode(19,0x81a4,0x100000001,0)
directory(21,[(2,'.',2),(2,'..',2),(12,'Documents',2),(13,'hello.txt',1),(15,'device',3),(16,'link',7),(17,'CASE.txt',1),(18,'case.txt',1),(19,'large-sparse.bin',1)])
directory(22,[(12,'.',2),(2,'..',2),(14,'Čeština.txt',1)])
with gzip.open(out/'minimal.ext2.gz','wb') as f:f.write(image)
# MBR: first Linux partition begins at sector 2048.
mbr=bytearray(3*1024*1024);mbr[510:512]=b'\x55\xaa';mbr[450]=0x83;put(mbr,454,'II',2048,len(image)//512);mbr[1024*1024:2*1024*1024]=image
with gzip.open(out/'mbr.ext2.gz','wb') as f:f.write(mbr)
# GPT fixtures cover 512-byte and native 4096-byte logical sectors.
for sector in (512,4096):
 size=4*1024*1024;disk=bytearray(size);last=size//sector-1;first=1024*1024//sector;count=len(image)//sector
 disk[510:512]=b'\x55\xaa';disk[450]=0xee;put(disk,454,'II',1,last)
 entries=bytearray(128*128);entries[0:16]=uuid.UUID('0fc63daf-8483-4772-8e79-3d69d8477de4').bytes_le;entries[16:32]=uuid.UUID(int=5).bytes_le;put(entries,32,'QQ',first,first+count-1);entries[56:76]='Linux demo'.encode('utf-16-le')
 blocks=len(entries)//sector;backup=last-blocks
 disk[2*sector:2*sector+len(entries)]=entries;disk[backup*sector:backup*sector+len(entries)]=entries
 for lba,other,table in ((1,last,2),(last,1,backup)):
  header=bytearray(92);header[:8]=b'EFI PART';put(header,8,'II',0x10000,92);put(header,24,'QQQQ',lba,other,2+blocks,backup-1);header[56:72]=uuid.UUID(int=9).bytes_le;put(header,72,'QIII',table,128,128,zlib.crc32(entries));put(header,16,'I',zlib.crc32(header));disk[lba*sector:lba*sector+92]=header
 disk[first*sector:first*sector+len(image)]=image
 with gzip.open(out/('gpt%d.ext2.gz'%sector),'wb') as f:f.write(disk)
print('Generated synthetic ext2, MBR and GPT (512/4096) fixtures')
