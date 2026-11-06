#include "megasdk_c.h"

#include <megaapi.h>

#include <atomic>
#include <cstring>
#include <memory>
#include <mutex>
#include <unordered_set>

using namespace mega;

namespace
{
class ListenerBase;

}

struct mega_api
{
    std::unique_ptr<MegaApi> api;
    std::mutex listenersMutex;
    std::unordered_set<ListenerBase*> listeners;
};

namespace
{
MegaNode* AsNode(const mega_node_t* node)
{
    return reinterpret_cast<MegaNode*>(const_cast<mega_node_t*>(node));
}

mega_node_t* ToNode(MegaNode* node)
{
    return reinterpret_cast<mega_node_t*>(node);
}

const MegaRequest* AsRequest(const mega_request_t* request)
{
    return reinterpret_cast<const MegaRequest*>(request);
}

const MegaTransfer* AsTransfer(const mega_transfer_t* transfer)
{
    return reinterpret_cast<const MegaTransfer*>(transfer);
}

MegaCancelToken* AsCancelToken(mega_cancel_token_t* token)
{
    return reinterpret_cast<MegaCancelToken*>(token);
}

char* CopyString(const char* value)
{
    if (!value)
    {
        return nullptr;
    }

    const auto length = std::strlen(value);
    auto* copy = new char[length + 1];
    std::memcpy(copy, value, length + 1);
    return copy;
}

// Listeners are single-use: they unregister and delete themselves once their operation finishes. Any still
// registered when the api is destroyed are deleted by mega_api_destroy.
class ListenerBase
{
public:
    explicit ListenerBase(mega_api_t* owner) : mOwner(owner)
    {
        std::lock_guard lock(mOwner->listenersMutex);
        mOwner->listeners.insert(this);
    }

    virtual ~ListenerBase() = default;

protected:
    void Complete()
    {
        {
            std::lock_guard lock(mOwner->listenersMutex);
            if (mOwner->listeners.erase(this) == 0)
            {
                // mega_api_destroy has taken ownership and will delete this listener.
                return;
            }
        }

        delete this;
    }

private:
    mega_api_t* mOwner;
};

class RequestListener final : public ListenerBase, public MegaRequestListener
{
public:
    RequestListener(mega_api_t* owner, mega_request_finish_cb callback, void* context)
        : ListenerBase(owner), mCallback(callback), mContext(context)
    {
    }

    void onRequestFinish(MegaApi*, MegaRequest* request, MegaError* error) override
    {
        if (mCallback)
        {
            mCallback(mContext,
                      reinterpret_cast<const mega_request_t*>(request),
                      error ? error->getErrorCode() : MegaError::API_OK,
                      error ? error->getErrorString() : nullptr);
        }

        Complete();
    }

private:
    mega_request_finish_cb mCallback;
    void* mContext;
};

class TransferListener final : public ListenerBase, public MegaTransferListener
{
public:
    TransferListener(mega_api_t* owner, const mega_transfer_callbacks_t& callbacks)
        : ListenerBase(owner), mCallbacks(callbacks)
    {
    }

    void onTransferStart(MegaApi*, MegaTransfer* transfer) override
    {
        if (mCallbacks.on_start)
        {
            mCallbacks.on_start(mCallbacks.context, reinterpret_cast<const mega_transfer_t*>(transfer));
        }
    }

    void onTransferUpdate(MegaApi*, MegaTransfer* transfer) override
    {
        if (mCallbacks.on_update)
        {
            mCallbacks.on_update(mCallbacks.context, reinterpret_cast<const mega_transfer_t*>(transfer));
        }
    }

    void onTransferTemporaryError(MegaApi*, MegaTransfer* transfer, MegaError* error) override
    {
        if (mCallbacks.on_temporary_error)
        {
            mCallbacks.on_temporary_error(mCallbacks.context,
                                          reinterpret_cast<const mega_transfer_t*>(transfer),
                                          error ? error->getErrorCode() : MegaError::API_OK,
                                          error ? error->getErrorString() : nullptr);
        }
    }

    void onTransferFinish(MegaApi*, MegaTransfer* transfer, MegaError* error) override
    {
        // Child transfers of a folder download report a positive folder transfer tag. Only the transfer that was
        // started (-1 for a folder, 0 for a file) completes the operation.
        if (transfer && transfer->getFolderTransferTag() > 0)
        {
            return;
        }

        if (mCallbacks.on_finish)
        {
            mCallbacks.on_finish(mCallbacks.context,
                                 reinterpret_cast<const mega_transfer_t*>(transfer),
                                 error ? error->getErrorCode() : MegaError::API_OK,
                                 error ? error->getErrorString() : nullptr);
        }

        Complete();
    }

private:
    mega_transfer_callbacks_t mCallbacks;
};

class CallbackLogger final : public MegaLogger
{
public:
    explicit CallbackLogger(mega_log_cb callback) : mCallback(callback)
    {
    }

    void log(const char* time, int logLevel, const char* source, const char* message
#ifdef ENABLE_LOG_PERFORMANCE
             , const char**, size_t*, int
#endif
             ) override
    {
        if (mCallback)
        {
            mCallback(time, logLevel, source, message);
        }
    }

private:
    mega_log_cb mCallback;
};

std::mutex gLoggerMutex;
std::unique_ptr<CallbackLogger> gLogger;
}

/* Library */

void mega_string_free(char* value)
{
    delete[] value;
}

void mega_set_log_level(int log_level)
{
    MegaApi::setLogLevel(log_level);
}

void mega_set_logger(mega_log_cb callback)
{
    std::lock_guard lock(gLoggerMutex);

    if (gLogger)
    {
        MegaApi::removeLoggerObject(gLogger.get());
        gLogger.reset();
    }

    if (callback)
    {
        gLogger = std::make_unique<CallbackLogger>(callback);
        MegaApi::addLoggerObject(gLogger.get());
    }
}

mega_handle_t mega_base64_to_handle(const char* base64_handle)
{
    return MegaApi::base64ToHandle(base64_handle);
}

char* mega_handle_to_base64(mega_handle_t handle)
{
    return MegaApi::handleToBase64(handle);
}

const char* mega_error_to_string(int error_code)
{
    return MegaError::getErrorString(error_code);
}

/* MegaApi */

mega_api_t* mega_api_create(const char* app_key,
                            const char* base_path,
                            const char* user_agent,
                            unsigned worker_thread_count)
{
    try
    {
        auto* wrapper = new mega_api();
        wrapper->api = std::make_unique<MegaApi>(app_key,
                                                 base_path,
                                                 user_agent,
                                                 worker_thread_count == 0 ? 1 : worker_thread_count);
        return wrapper;
    }
    catch (...)
    {
        return nullptr;
    }
}

void mega_api_destroy(mega_api_t* api)
{
    if (!api)
    {
        return;
    }

    // No listener callbacks run once ~MegaApi returns.
    api->api.reset();

    std::unordered_set<ListenerBase*> remaining;
    {
        std::lock_guard lock(api->listenersMutex);
        remaining.swap(api->listeners);
    }

    for (auto* listener : remaining)
    {
        delete listener;
    }

    delete api;
}

int mega_api_is_logged_in(mega_api_t* api)
{
    return api->api->isLoggedIn();
}

void mega_api_login(mega_api_t* api,
                    const char* email,
                    const char* password,
                    mega_request_finish_cb callback,
                    void* context)
{
    api->api->login(email, password, new RequestListener(api, callback, context));
}

void mega_api_login_to_folder(mega_api_t* api,
                              const char* folder_link,
                              mega_request_finish_cb callback,
                              void* context)
{
    api->api->loginToFolder(folder_link, new RequestListener(api, callback, context));
}

void mega_api_fetch_nodes(mega_api_t* api, mega_request_finish_cb callback, void* context)
{
    api->api->fetchNodes(new RequestListener(api, callback, context));
}

void mega_api_logout(mega_api_t* api, mega_request_finish_cb callback, void* context)
{
    api->api->logout(new RequestListener(api, callback, context));
}

void mega_api_local_logout(mega_api_t* api, mega_request_finish_cb callback, void* context)
{
    api->api->localLogout(new RequestListener(api, callback, context));
}

void mega_api_get_public_node(mega_api_t* api,
                              const char* file_link,
                              mega_request_finish_cb callback,
                              void* context)
{
    api->api->getPublicNode(file_link, new RequestListener(api, callback, context));
}

mega_node_t* mega_api_get_root_node(mega_api_t* api)
{
    return ToNode(api->api->getRootNode());
}

mega_node_t* mega_api_get_node_by_handle(mega_api_t* api, mega_handle_t handle)
{
    return ToNode(api->api->getNodeByHandle(handle));
}

mega_node_t* mega_api_get_parent_node(mega_api_t* api, const mega_node_t* node)
{
    return ToNode(api->api->getParentNode(AsNode(node)));
}

mega_node_list_t* mega_api_get_children(mega_api_t* api, const mega_node_t* parent)
{
    return reinterpret_cast<mega_node_list_t*>(api->api->getChildren(AsNode(parent)));
}

char* mega_api_get_node_path(mega_api_t* api, const mega_node_t* node)
{
    return api->api->getNodePath(AsNode(node));
}

mega_node_t* mega_api_authorize_node(mega_api_t* api, const mega_node_t* node)
{
    return ToNode(api->api->authorizeNode(AsNode(node)));
}

void mega_api_start_download(mega_api_t* api,
                             const mega_node_t* node,
                             const char* local_path,
                             const char* custom_name,
                             mega_cancel_token_t* cancel_token,
                             const mega_transfer_callbacks_t* callbacks)
{
    mega_transfer_callbacks_t effective{};
    if (callbacks)
    {
        effective = *callbacks;
    }

    api->api->startDownload(AsNode(node),
                            local_path,
                            custom_name,
                            nullptr,
                            false,
                            AsCancelToken(cancel_token),
                            MegaTransfer::COLLISION_CHECK_FINGERPRINT,
                            MegaTransfer::COLLISION_RESOLUTION_NEW_WITH_N,
                            false,
                            new TransferListener(api, effective));
}

/* MegaRequest */

int mega_request_get_type(const mega_request_t* request)
{
    return AsRequest(request)->getType();
}

mega_handle_t mega_request_get_node_handle(const mega_request_t* request)
{
    return AsRequest(request)->getNodeHandle();
}

int mega_request_get_flag(const mega_request_t* request)
{
    return AsRequest(request)->getFlag() ? 1 : 0;
}

mega_node_t* mega_request_copy_public_node(const mega_request_t* request)
{
    return ToNode(AsRequest(request)->getPublicMegaNode());
}

/* MegaTransfer */

int mega_transfer_get_tag(const mega_transfer_t* transfer)
{
    return AsTransfer(transfer)->getTag();
}

int mega_transfer_get_state(const mega_transfer_t* transfer)
{
    return AsTransfer(transfer)->getState();
}

int mega_transfer_get_stage(const mega_transfer_t* transfer)
{
    return static_cast<int>(AsTransfer(transfer)->getStage());
}

int mega_transfer_is_folder(const mega_transfer_t* transfer)
{
    return AsTransfer(transfer)->isFolderTransfer() ? 1 : 0;
}

int mega_transfer_get_folder_transfer_tag(const mega_transfer_t* transfer)
{
    return AsTransfer(transfer)->getFolderTransferTag();
}

int64_t mega_transfer_get_transferred_bytes(const mega_transfer_t* transfer)
{
    return AsTransfer(transfer)->getTransferredBytes();
}

int64_t mega_transfer_get_total_bytes(const mega_transfer_t* transfer)
{
    return AsTransfer(transfer)->getTotalBytes();
}

int64_t mega_transfer_get_speed(const mega_transfer_t* transfer)
{
    return AsTransfer(transfer)->getSpeed();
}

mega_handle_t mega_transfer_get_node_handle(const mega_transfer_t* transfer)
{
    return AsTransfer(transfer)->getNodeHandle();
}

const char* mega_transfer_get_file_name(const mega_transfer_t* transfer)
{
    return AsTransfer(transfer)->getFileName();
}

const char* mega_transfer_get_path(const mega_transfer_t* transfer)
{
    return AsTransfer(transfer)->getPath();
}

/* MegaNode */

void mega_node_free(mega_node_t* node)
{
    delete AsNode(node);
}

mega_node_t* mega_node_copy(const mega_node_t* node)
{
    return ToNode(AsNode(node)->copy());
}

int mega_node_get_type(const mega_node_t* node)
{
    return AsNode(node)->getType();
}

const char* mega_node_get_name(const mega_node_t* node)
{
    return AsNode(node)->getName();
}

mega_handle_t mega_node_get_handle(const mega_node_t* node)
{
    return AsNode(node)->getHandle();
}

mega_handle_t mega_node_get_parent_handle(const mega_node_t* node)
{
    return AsNode(node)->getParentHandle();
}

int64_t mega_node_get_size(const mega_node_t* node)
{
    return AsNode(node)->getSize();
}

int64_t mega_node_get_creation_time(const mega_node_t* node)
{
    return AsNode(node)->getCreationTime();
}

int64_t mega_node_get_modification_time(const mega_node_t* node)
{
    return AsNode(node)->getModificationTime();
}

const char* mega_node_get_fingerprint(const mega_node_t* node)
{
    return AsNode(node)->getFingerprint();
}

/* MegaNodeList */

void mega_node_list_free(mega_node_list_t* list)
{
    delete reinterpret_cast<MegaNodeList*>(list);
}

int mega_node_list_size(const mega_node_list_t* list)
{
    return reinterpret_cast<const MegaNodeList*>(list)->size();
}

mega_node_t* mega_node_list_copy_at(const mega_node_list_t* list, int index)
{
    auto* node = reinterpret_cast<const MegaNodeList*>(list)->get(index);
    return node ? ToNode(node->copy()) : nullptr;
}

/* MegaCancelToken */

mega_cancel_token_t* mega_cancel_token_create(void)
{
    return reinterpret_cast<mega_cancel_token_t*>(MegaCancelToken::createInstance());
}

void mega_cancel_token_cancel(mega_cancel_token_t* token)
{
    AsCancelToken(token)->cancel();
}

void mega_cancel_token_free(mega_cancel_token_t* token)
{
    delete AsCancelToken(token);
}
