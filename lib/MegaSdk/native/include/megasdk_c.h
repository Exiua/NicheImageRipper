/*
 * Flat C ABI over the MEGA SDK's C++ MegaApi, consumed by the MegaSdk C# project via P/Invoke.
 *
 * Ownership rules:
 *   - mega_node_t / mega_node_list_t / mega_cancel_token_t returned from functions are owned by the caller and
 *     must be released with their *_free function.
 *   - char* returned from mega_*_copy_* / mega_handle_to_base64 must be released with mega_string_free.
 *   - const char* returned from getters are owned by the SDK object and are only valid while it is alive.
 *   - mega_request_t / mega_transfer_t passed to callbacks are only valid for the duration of the callback.
 *
 * Callbacks run on SDK threads.
 */
#ifndef MEGASDK_C_H
#define MEGASDK_C_H

#include <stdint.h>

#ifdef _WIN32
#define MEGASDK_C_API __declspec(dllexport)
#define MEGASDK_C_CALL __cdecl
#else
#define MEGASDK_C_API __attribute__((visibility("default")))
#define MEGASDK_C_CALL
#endif

#ifdef __cplusplus
extern "C" {
#endif

typedef uint64_t mega_handle_t;

typedef struct mega_api mega_api_t;
typedef struct mega_node mega_node_t;
typedef struct mega_node_list mega_node_list_t;
typedef struct mega_request mega_request_t;
typedef struct mega_transfer mega_transfer_t;
typedef struct mega_cancel_token mega_cancel_token_t;

typedef void (MEGASDK_C_CALL *mega_request_finish_cb)(void* context,
                                                      const mega_request_t* request,
                                                      int error_code,
                                                      const char* error_string);

typedef void (MEGASDK_C_CALL *mega_transfer_event_cb)(void* context,
                                                      const mega_transfer_t* transfer);

typedef void (MEGASDK_C_CALL *mega_transfer_error_cb)(void* context,
                                                      const mega_transfer_t* transfer,
                                                      int error_code,
                                                      const char* error_string);

typedef void (MEGASDK_C_CALL *mega_log_cb)(const char* time,
                                           int log_level,
                                           const char* source,
                                           const char* message);

typedef struct mega_transfer_callbacks
{
    void* context;
    mega_transfer_event_cb on_start;
    mega_transfer_event_cb on_update;
    mega_transfer_error_cb on_temporary_error;
    /* Called once, for the transfer that was started (not for child transfers of a folder download). */
    mega_transfer_error_cb on_finish;
} mega_transfer_callbacks_t;

/* Library */
MEGASDK_C_API void MEGASDK_C_CALL mega_string_free(char* value);
MEGASDK_C_API void MEGASDK_C_CALL mega_set_log_level(int log_level);
/* Pass NULL to remove the logger. */
MEGASDK_C_API void MEGASDK_C_CALL mega_set_logger(mega_log_cb callback);
MEGASDK_C_API mega_handle_t MEGASDK_C_CALL mega_base64_to_handle(const char* base64_handle);
MEGASDK_C_API char* MEGASDK_C_CALL mega_handle_to_base64(mega_handle_t handle);
MEGASDK_C_API const char* MEGASDK_C_CALL mega_error_to_string(int error_code);

/* MegaApi */
MEGASDK_C_API mega_api_t* MEGASDK_C_CALL mega_api_create(const char* app_key,
                                                         const char* base_path,
                                                         const char* user_agent,
                                                         unsigned worker_thread_count);
/* Pending callbacks are either delivered or dropped before this returns; none run afterwards. */
MEGASDK_C_API void MEGASDK_C_CALL mega_api_destroy(mega_api_t* api);
MEGASDK_C_API int MEGASDK_C_CALL mega_api_is_logged_in(mega_api_t* api);

MEGASDK_C_API void MEGASDK_C_CALL mega_api_login(mega_api_t* api,
                                                 const char* email,
                                                 const char* password,
                                                 mega_request_finish_cb callback,
                                                 void* context);
MEGASDK_C_API void MEGASDK_C_CALL mega_api_login_to_folder(mega_api_t* api,
                                                           const char* folder_link,
                                                           mega_request_finish_cb callback,
                                                           void* context);
MEGASDK_C_API void MEGASDK_C_CALL mega_api_fetch_nodes(mega_api_t* api,
                                                       mega_request_finish_cb callback,
                                                       void* context);
MEGASDK_C_API void MEGASDK_C_CALL mega_api_logout(mega_api_t* api,
                                                  mega_request_finish_cb callback,
                                                  void* context);
MEGASDK_C_API void MEGASDK_C_CALL mega_api_local_logout(mega_api_t* api,
                                                        mega_request_finish_cb callback,
                                                        void* context);
MEGASDK_C_API void MEGASDK_C_CALL mega_api_get_public_node(mega_api_t* api,
                                                           const char* file_link,
                                                           mega_request_finish_cb callback,
                                                           void* context);

MEGASDK_C_API mega_node_t* MEGASDK_C_CALL mega_api_get_root_node(mega_api_t* api);
MEGASDK_C_API mega_node_t* MEGASDK_C_CALL mega_api_get_node_by_handle(mega_api_t* api, mega_handle_t handle);
MEGASDK_C_API mega_node_t* MEGASDK_C_CALL mega_api_get_parent_node(mega_api_t* api, const mega_node_t* node);
MEGASDK_C_API mega_node_list_t* MEGASDK_C_CALL mega_api_get_children(mega_api_t* api, const mega_node_t* parent);
MEGASDK_C_API char* MEGASDK_C_CALL mega_api_get_node_path(mega_api_t* api, const mega_node_t* node);
/* Returns a copy of node that can be downloaded from a folder link session's nodes outside the session. */
MEGASDK_C_API mega_node_t* MEGASDK_C_CALL mega_api_authorize_node(mega_api_t* api, const mega_node_t* node);

/*
 * local_path: a directory ending in a path separator (the node's name is used), or a full file path.
 * cancel_token may be NULL. It must stay alive until on_finish is called.
 */
MEGASDK_C_API void MEGASDK_C_CALL mega_api_start_download(mega_api_t* api,
                                                          const mega_node_t* node,
                                                          const char* local_path,
                                                          const char* custom_name,
                                                          mega_cancel_token_t* cancel_token,
                                                          const mega_transfer_callbacks_t* callbacks);

/* MegaRequest (only valid inside a mega_request_finish_cb) */
MEGASDK_C_API int MEGASDK_C_CALL mega_request_get_type(const mega_request_t* request);
MEGASDK_C_API mega_handle_t MEGASDK_C_CALL mega_request_get_node_handle(const mega_request_t* request);
MEGASDK_C_API int MEGASDK_C_CALL mega_request_get_flag(const mega_request_t* request);
/* Caller owns the returned node. */
MEGASDK_C_API mega_node_t* MEGASDK_C_CALL mega_request_copy_public_node(const mega_request_t* request);

/* MegaTransfer (only valid inside a transfer callback) */
MEGASDK_C_API int MEGASDK_C_CALL mega_transfer_get_tag(const mega_transfer_t* transfer);
MEGASDK_C_API int MEGASDK_C_CALL mega_transfer_get_state(const mega_transfer_t* transfer);
MEGASDK_C_API int MEGASDK_C_CALL mega_transfer_get_stage(const mega_transfer_t* transfer);
MEGASDK_C_API int MEGASDK_C_CALL mega_transfer_is_folder(const mega_transfer_t* transfer);
MEGASDK_C_API int MEGASDK_C_CALL mega_transfer_get_folder_transfer_tag(const mega_transfer_t* transfer);
MEGASDK_C_API int64_t MEGASDK_C_CALL mega_transfer_get_transferred_bytes(const mega_transfer_t* transfer);
MEGASDK_C_API int64_t MEGASDK_C_CALL mega_transfer_get_total_bytes(const mega_transfer_t* transfer);
MEGASDK_C_API int64_t MEGASDK_C_CALL mega_transfer_get_speed(const mega_transfer_t* transfer);
MEGASDK_C_API mega_handle_t MEGASDK_C_CALL mega_transfer_get_node_handle(const mega_transfer_t* transfer);
MEGASDK_C_API const char* MEGASDK_C_CALL mega_transfer_get_file_name(const mega_transfer_t* transfer);
MEGASDK_C_API const char* MEGASDK_C_CALL mega_transfer_get_path(const mega_transfer_t* transfer);

/* MegaNode */
MEGASDK_C_API void MEGASDK_C_CALL mega_node_free(mega_node_t* node);
MEGASDK_C_API mega_node_t* MEGASDK_C_CALL mega_node_copy(const mega_node_t* node);
MEGASDK_C_API int MEGASDK_C_CALL mega_node_get_type(const mega_node_t* node);
MEGASDK_C_API const char* MEGASDK_C_CALL mega_node_get_name(const mega_node_t* node);
MEGASDK_C_API mega_handle_t MEGASDK_C_CALL mega_node_get_handle(const mega_node_t* node);
MEGASDK_C_API mega_handle_t MEGASDK_C_CALL mega_node_get_parent_handle(const mega_node_t* node);
MEGASDK_C_API int64_t MEGASDK_C_CALL mega_node_get_size(const mega_node_t* node);
MEGASDK_C_API int64_t MEGASDK_C_CALL mega_node_get_creation_time(const mega_node_t* node);
MEGASDK_C_API int64_t MEGASDK_C_CALL mega_node_get_modification_time(const mega_node_t* node);
MEGASDK_C_API const char* MEGASDK_C_CALL mega_node_get_fingerprint(const mega_node_t* node);

/* MegaNodeList */
MEGASDK_C_API void MEGASDK_C_CALL mega_node_list_free(mega_node_list_t* list);
MEGASDK_C_API int MEGASDK_C_CALL mega_node_list_size(const mega_node_list_t* list);
/* Caller owns the returned node. */
MEGASDK_C_API mega_node_t* MEGASDK_C_CALL mega_node_list_copy_at(const mega_node_list_t* list, int index);

/* MegaCancelToken */
MEGASDK_C_API mega_cancel_token_t* MEGASDK_C_CALL mega_cancel_token_create(void);
MEGASDK_C_API void MEGASDK_C_CALL mega_cancel_token_cancel(mega_cancel_token_t* token);
MEGASDK_C_API void MEGASDK_C_CALL mega_cancel_token_free(mega_cancel_token_t* token);

#ifdef __cplusplus
}
#endif

#endif
