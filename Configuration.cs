using LazyAPI.Attributes;
using LazyAPI.ConfigFiles;

namespace Chameleon;

[Config]
internal class Configuration : JsonConfigBase<Configuration>
{
    protected override string Filename => "Chameleon";

    [LocalizedPropertyName(CultureType.English, "VerifyloginIP", Order = -3)]
    public bool VerifyloginIP { get; set; } = false;

    [LocalizedPropertyName(CultureType.English, "AwaitBufferSize")]
    public ushort AwaitBufferSize { get; set; } = Chameleon.Size;

    [LocalizedPropertyName(CultureType.English, "EnableForcedHint")]
    public bool EnableForcedHint { get; set; } = true;

    [LocalizedPropertyName(CultureType.English, "Greeting")]
    public string Greeting { get; set; } = "   Welcome to the Server!";

    [LocalizedPropertyName(CultureType.English, "VerificationFailedMessage")]
    public string VerificationFailedMessage { get; set; } = "         Incorrect account password.\r\n\r\n         If this is your first time joining, please use a different character name;\r\n         If you forgot your password, please contact an administrator.";

    [LocalizedPropertyName(CultureType.English, "Hints")]
    public string[] Hints { get; set; } = new[]
    {
        " ↓↓ Please read the instructions below to join the server ↓↓",
        " \r\n         Click after reading below →",
        " 1. Enter your password in \"Server Password\". You will use this password when joining in the future.\nThis sets a password for your account.",
        " 2. After reading, please reconnect."
    };

    [LocalizedPropertyName(CultureType.Chinese, "同步注册服务器")]
    [LocalizedPropertyName(CultureType.English, "SyncServerReg")]
    public List<RESTser> SyncServerReg { get; set; } = new List<RESTser>
    {
        new RESTser
        {
            Name = "Test Server",
            IPAddress = "http://127.0.0.1:7878/",
            Token = "TOKENTEST"
        }
    };

    public class RESTser
    {
        [LocalizedPropertyName(CultureType.English, "Name")]
        public string Name { get; set; } = "";

        [LocalizedPropertyName(CultureType.Chinese, "地址")]
        [LocalizedPropertyName(CultureType.English, "IpAddress")]
        public string IPAddress { get; set; } = "";

        public string Token { get; set; } = "";
    }

    protected override void SetDefault()
    {
        this.AwaitBufferSize = Chameleon.Size;
        this.EnableForcedHint = true;
        this.Greeting = "   Welcome to the Server!";
        this.VerificationFailedMessage = "         Incorrect account password.\r\n\r\n         If this is your first time joining, please use a different character name;\r\n         If you forgot your password, please contact an administrator.";
        this.Hints = new[]
        {
            " ↓↓ Please read the instructions below to join the server ↓↓",
            " \r\n         Click after reading below →",
            " 1. Enter your password in \"Server Password\". You will use this password when joining in the future. This sets a password for your account.",
            " 2. After reading, please reconnect."
        };
        this.SyncServerReg = new List<RESTser>
        {
            new RESTser
            {
                Name = "Test Server",
                IPAddress = "http://127.0.0.1:7878/",
                Token = "TOKENTEST"
            }
        };
    }
}