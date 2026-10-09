namespace ImpersonateClient;

internal enum CurlHttpVersion : int
{
    None = 0,
    V1_0 = 1,
    V1_1 = 2,
    V2_0 = 3,
    V2Tls = 4,
    V2PriorKnowledge = 5,
    V3 = 30,
}