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
