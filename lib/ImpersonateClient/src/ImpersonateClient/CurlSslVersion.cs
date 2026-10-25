namespace ImpersonateClient;

[Flags]
internal enum CurlSslVersion : int
{
    Default = 0,
    TlsV1 = 1,
    SslV2 = 2,
    SslV3 = 3,
    TlsV1_0 = 4,
    TlsV1_1 = 5,
    TlsV1_2 = 6,
    TlsV1_3 = 7,

    MaxDefault = 1 << 16,
    MaxTlsV1_0 = 4 << 16,
    MaxTlsV1_1 = 5 << 16,
    MaxTlsV1_2 = 6 << 16,
    MaxTlsV1_3 = 7 << 16,
}