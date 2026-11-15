/* NFS transfer primitives. Each operation uses an isolated context. */
#include <winsock2.h>
#include <fcntl.h>
#include <stdio.h>
#include <string.h>
#include <nfsc/libnfs.h>
extern int fc_nfs_client_identity(struct nfs_context *ctx);
typedef int (__cdecl *fc_nfs_data)(unsigned char *, int, void *);
static int fc_connect(const char *host,const char *share,int version,struct nfs_context **context,char *error,int size) {
  WSADATA wsa; int r;
  if (WSAStartup(MAKEWORD(2,2),&wsa)) {snprintf(error,size,"Winsock initialization failed");return -1;}
  *context=nfs_init_context();
  if(!*context){snprintf(error,size,"NFS initialization failed");WSACleanup();return -1;}
  if(version != 3 && fc_nfs_client_identity(*context)<0){snprintf(error,size,"Cannot create NFS client identity");nfs_destroy_context(*context);*context=NULL;WSACleanup();return -1;}
  r=nfs_set_version(*context,version);
  if(r>=0)r=nfs_mount(*context,host,share);
  if(r<0) {snprintf(error,size,"%s",nfs_get_error(*context)); nfs_destroy_context(*context);*context=NULL;WSACleanup();return r;}
  return 0;
}
static void fc_done(struct nfs_context *ctx){nfs_destroy_context(ctx);WSACleanup();}
__declspec(dllexport) int cn_nfs_read(const char *host,const char *share,int version,const char *path,
 uint64_t start, uint64_t length,fc_nfs_data consumer,void *state,char *error,int size){
 struct nfs_context *ctx=NULL;struct nfsfh *file=NULL;int r=-1;
 if(!consumer||!error||size<2)return -1;error[0]=0;
 r=fc_connect(host,share,version,&ctx,error,size);if(r<0)return r;
 r=nfs_open(ctx,path,O_RDONLY,&file);if(r<0)goto failed;
 unsigned char buf[65536];uint64_t done=0;
 while(done<length){
    size_t count=(size_t)((length-done)>sizeof(buf)?sizeof(buf):length-done);
    r=nfs_pread(ctx,file,buf,count,start+done);
    if(r<=0){if(r==0)snprintf(error,size,"File ended before expected length");goto failed;}
    if(consumer(buf,r,state)!=0){snprintf(error,size,"Transfer aborted by application");r=-1;goto failed;}
    done+=(uint64_t)r;
 }
 r=0; goto finished;
failed:
 if(!error[0])snprintf(error,size,"%s",nfs_get_error(ctx));
finished:
 if(file){int cr=nfs_close(ctx,file);if(!r&&cr<0){r=cr;snprintf(error,size,"%s",nfs_get_error(ctx));}}
 fc_done(ctx);return r;
}
__declspec(dllexport) int cn_nfs_write(const char *host,const char *share,int version,const char *path,
 uint64_t length,fc_nfs_data producer,void *state,char *error,int size){
 struct nfs_context *ctx=NULL;struct nfsfh *file=NULL;int r=-1,created=0;
 if(!producer||!error||size<2)return -1;error[0]=0;
 r=fc_connect(host,share,version,&ctx,error,size);if(r<0)return r;
 r=nfs_open2(ctx,path,O_WRONLY|O_CREAT|O_EXCL,0644,&file);
 if(r<0)goto failed;
 created=1;
 unsigned char buf[65536];uint64_t done=0;
 while(done<length){
   int count=(int)((length-done)>sizeof(buf)?sizeof(buf):length-done);
   int got=producer(buf,count,state);
   if(got<=0||got>count){r=-1;snprintf(error,size,"Local source ended or transfer cancelled");goto failed;}
   int written=0;
   while(written<got){r=nfs_pwrite(ctx,file,buf+written,got-written,done+written);if(r<=0)goto failed;written+=r;}
   done+=(uint64_t)got;
 }
 r=nfs_fsync(ctx,file);
 if(r<0)goto failed;
 r=0;goto finished;
failed:
 if(!error[0])snprintf(error,size,"%s",nfs_get_error(ctx));
finished:
 if(file){int cr=nfs_close(ctx,file);if(!r&&cr<0){r=cr;snprintf(error,size,"%s",nfs_get_error(ctx));}}
 if(r<0&&created)nfs_unlink(ctx,path);
 fc_done(ctx);return r;
}
__declspec(dllexport) int cn_nfs_action(const char *host,const char *share,int version,int action,
 const char *first,const char *second,char *error,int size) {
 struct nfs_context *ctx=NULL;int r;
 if(!error||size<2)return -1;error[0]=0;
 r=fc_connect(host,share,version,&ctx,error,size);if(r<0)return r;
 switch(action){
 case 1: r=nfs_rename(ctx,first,second);break;
 case 2: r=nfs_unlink(ctx,first);break;
 case 3: r=nfs_mkdir(ctx,first);break;
 case 4: r=nfs_rmdir(ctx,first);break;
 default: r=-1;snprintf(error,size,"Invalid NFS operation");break;
 }
 if(r<0&&!error[0])snprintf(error,size,"%s",nfs_get_error(ctx));
 fc_done(ctx);return r;
}
