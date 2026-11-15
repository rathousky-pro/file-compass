#define CURL_STATICLIB
#include <curl/curl.h>
#include <libssh2.h>
#include <stdlib.h>
#include <string.h>
__declspec(dllexport) int cn_setopt_long(CURL *c, int option, long value) { return curl_easy_setopt(c, (CURLoption)option, value); }
__declspec(dllexport) int cn_setopt_ptr(CURL *c, int option, void *value) { return curl_easy_setopt(c, (CURLoption)option, value); }
__declspec(dllexport) int cn_setopt_off(CURL *c, int option, long long value) { return curl_easy_setopt(c, (CURLoption)option, (curl_off_t)value); }
__declspec(dllexport) void *cn_alloc(size_t length) { return malloc(length); }
__declspec(dllexport) LIBSSH2_CHANNEL* cn_scp_recv(LIBSSH2_SESSION* session,const char* path,long long* size) {
    libssh2_struct_stat attributes;
    LIBSSH2_CHANNEL* channel=libssh2_scp_recv2(session,path,&attributes);
    if(channel) *size=(long long)attributes.st_size;
    return channel;
}

/* NFS read-only browsing bridge. Keep all libnfs ownership native. */
#include <winsock2.h>
#include <nfsc/libnfs.h>
#include <windows.h>
#include <bcrypt.h>
#include <stdio.h>
/* Never reuse an NFSv4 client identity when a short-lived context is recycled. */
int fc_nfs_client_identity(struct nfs_context *ctx) {
    unsigned char id[16]; char name[48];
    if (BCryptGenRandom(NULL,id,sizeof(id),BCRYPT_USE_SYSTEM_PREFERRED_RNG) != 0) return -1;
    snprintf(name,sizeof(name),"FileCompass-%02x%02x%02x%02x%02x%02x%02x%02x%02x%02x%02x%02x%02x%02x%02x%02x",
        id[0],id[1],id[2],id[3],id[4],id[5],id[6],id[7],id[8],id[9],id[10],id[11],id[12],id[13],id[14],id[15]);
    nfs4_set_client_name(ctx,name); return 0;
}
typedef void (__cdecl *cn_nfs_item)(const char*, int, unsigned long long, long long, void*);
static int cn_nfs_sock_init(void) {
    WSADATA wsa;
    return WSAStartup(MAKEWORD(2,2), &wsa);
}
__declspec(dllexport) int cn_nfs_list(const char *host, const char *exportpath,
    int version, const char *path, cn_nfs_item callback, void *user,
    char *error, int errorlen) {
    struct nfs_context *ctx = NULL;
    struct nfsdir *dir = NULL;
    struct nfsdirent *item = NULL;
    int result = -1, err = 0;
    if (!host || !exportpath || !path || !callback || !error || errorlen < 2) return -1;
    error[0] = 0;
    if (cn_nfs_sock_init()) { strncpy(error, "WSAStartup failed", errorlen-1); error[errorlen-1]=0; return -1; }
    ctx = nfs_init_context();
    if (!ctx) { strncpy(error, "Cannot initialize NFS context", errorlen-1); error[errorlen-1]=0; WSACleanup(); return -1; }
    if (version != 3 && fc_nfs_client_identity(ctx) < 0) { strncpy(error,"Cannot create NFS client identity",errorlen-1); error[errorlen-1]=0; goto finish; }
    if (nfs_set_version(ctx, version) < 0) goto failure;
    if ((err = nfs_mount(ctx, host, exportpath)) < 0) goto failure;
    if ((err = nfs_opendir(ctx, path, &dir)) < 0) goto failure;
    while ((item = nfs_readdir(ctx, dir))) {
        if (item->name) callback(item->name, item->type == 2 ? 1 : item->type == 5 ? 2 : 0, item->size,
            (long long)item->mtime.tv_sec, user);
    }
    result = 0;
    goto finish;
failure:
    {
        const char *message = nfs_get_error(ctx);
        if (!message || !*message) message = "NFS request failed";
        strncpy(error, message, errorlen - 1);
        error[errorlen-1] = 0;
    }
finish:
    if (dir) nfs_closedir(ctx,dir);
    nfs_destroy_context(ctx);
    WSACleanup();
    return result;
}
