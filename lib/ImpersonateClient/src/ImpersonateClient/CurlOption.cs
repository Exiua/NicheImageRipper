namespace ImpersonateClient;

internal enum CurlOption : int
{
    // Standard libcurl options
    Url = 10002,
    Proxy = 10004,
    WriteData = 10001,
    WriteFunction = 20011,
    CustomRequest = 10036,
    HttpHeader = 10023,

    Timeout = 13,
    SslVersion = 32,
    Nobody = 44,
    Upload = 46,
    Post = 47,
    FollowLocation = 52,
    SslVerifyPeer = 64,
    HttpGet = 80,
    SslVerifyHost = 81,
    HttpVersion = 84,
    StreamWeight = 239,
    SslEnableAlpn = 226,

    Username = 10173,
    Password = 10174,
    CopyPostFields = 10165,
    SslCipherList = 10083,
    PostFieldSizeLarge = 30120,

    // curl-impersonate / curl_cffi custom options
    SslSigHashAlgs = 11001,
    SslCertCompression = 11003,
    Http2PseudoHeadersOrder = 11005,
    Http2Settings = 11006,
    Http2Streams = 11010,
    TlsExtensionOrder = 11012,
    TlsDelegatedCredentials = 11017,
    Ech = 10325,

    SslEnableAlps = 1002,
    SslEnableTicket = 1004,
    SslPermuteExtensions = 1007,
    Http2WindowUpdate = 10008,
    TlsGrease = 1011,
    StreamExclusive = 1013,
    TlsSignedCertTimestamps = 1015,
    TlsStatusRequest = 1016,
    TlsRecordSizeLimit = 1018,
    Http2NoPriority = 1021,

    HeaderData = 10029,      // CURLOPT_HEADERDATA
    HeaderFunction = 20079,  // CURLOPT_HEADERFUNCTION
    
    CaInfo = 10065, // CURLOPT_CAINFO
    CaPath = 10097, // CURLOPT_CAPAT
    AcceptEncoding = 10102, // CURLOPT_ACCEPT_ENCODING
}