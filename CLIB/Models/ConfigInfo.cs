namespace CLIB.Models;

public class ConfigInfo
{
    public string key = "Nn95IgGUrkYxjz4KmHTysuAVPvtBb6XF";
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ClientAppId { get; set; } = string.Empty;
    public string ClientVersion { get; set; } = string.Empty;
    public uint ProtocolVersionMinor { get; set; }
    public uint ProtocolVersionMajor { get; set; }

    public string? adminSdk { get; set; }
    public string? ProjectKey { get; set; }
    public string Url { get; set; } = string.Empty;
    public string ServiceAccount { get; set; } = string.Empty;
    public string ProjID { get; set; } = string.Empty;
    public string MIP { get; set; } = string.Empty;
    public int MPort { get; set; }

}
