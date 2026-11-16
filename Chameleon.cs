using LazyAPI;
using Newtonsoft.Json;
using System.IO.Streams;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.DB;
using TShockAPI.Hooks;

namespace Chameleon;

[ApiVersion(2, 1)]
public class Chameleon : LazyPlugin
{
    private const string WaitPwd4Reg = "reg-pwd";

    public const ushort Size = 10;

    public static readonly List<string> PrepareList = new(); // Players who received the announcement and are waiting to register

    public override string Name => System.Reflection.Assembly.GetExecutingAssembly().GetName().Name!;
    public override string Author => "mistzzt, GandiXien";

    public override string Description => GetString("Account system interactive replacement solution");

    public override Version Version => new Version(1, 1, 2);


    public Chameleon(Main game) : base(game)
    {
    }

    public override void Initialize()
    {
        ServerApi.Hooks.NetGetData.Register(this, OnGetData, int.MaxValue);
        ServerApi.Hooks.GamePostInitialize.Register(this, OnPostInit, int.MaxValue);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);
            ServerApi.Hooks.GamePostInitialize.Deregister(this, OnPostInit);
        }
        base.Dispose(disposing);
    }

    private static void OnPostInit(EventArgs args)
    {
        if (!string.IsNullOrEmpty(TShock.Config.Settings.ServerPassword) || !string.IsNullOrEmpty(Netplay.ServerPassword))
        {
            TShock.Log.ConsoleError(GetString("[Chameleon] With this plugin enabled, the native server password feature will be disabled."));
        }

        if (TShock.Config.Settings.DisableLoginBeforeJoin)
        {
            TShock.Log.ConsoleError(GetString("[Chameleon] With this plugin enabled, login before joining will be forcibly enabled."));
            TShock.Config.Settings.DisableLoginBeforeJoin = true;
        }

        if (!TShock.Config.Settings.RequireLogin && !TShock.ServerSideCharacterConfig.Settings.Enabled)
        {
            TShock.Log.ConsoleError(GetString("[Chameleon] With this plugin enabled, registration and login will be forcibly enabled."));
            TShock.Config.Settings.RequireLogin = true;
        }
    }

    private static void OnGetData(GetDataEventArgs args)
    {
        if (args.Handled)
        {
            return;
        }

        var type = args.MsgID;

        var player = TShock.Players[args.Msg.whoAmI];

        if (player == null)
        {
            return;
        }

        if (player.IsLoggedIn)
        {
            return;
        }

        if (player.RequiresPassword && type != PacketTypes.PasswordSend)
        {
            args.Handled = true;
            return;
        }

        if (type is PacketTypes.ContinueConnecting2 or PacketTypes.PasswordSend)
        {
            using var data = new MemoryStream(args.Msg.readBuffer, args.Index, args.Length - 1);
            args.Handled = type == PacketTypes.ContinueConnecting2 ? HandleConnecting(player) : HandlePassword(player, data.ReadString());
        }
    }

    private static bool HandleConnecting(TSPlayer player)
    {
        var account = TShock.UserAccounts.GetUserAccountByName(player.Name);
        if (account is null)
        {
            // Only unregistered players will see the forced hint
            if (Configuration.Instance.EnableForcedHint && !PrepareList.Contains(player.Name))
            {
                AddToList(player.Name);
                Kick(player, string.Join("\n", Configuration.Instance.Hints), Configuration.Instance.Greeting);
                return true;
            }

            // Unregistered part.1
            player.SetData(WaitPwd4Reg, true);
            NetMessage.SendData((int) PacketTypes.PasswordRequired, player.Index);
            return true;
        }

        player.DataWhenJoined = new PlayerData(true);
        player.DataWhenJoined.CopyCharacter(player);

        do
        {
            // UUID Auto-login Registered part.2
            if (TShock.Config.Settings.DisableUUIDLogin)
            {
                TShock.Log.ConsoleInfo(GetString($"[Chameleon] `DisableUUIDLogin` is set to true. Player `{player.Name}` is not allowed to log in via UUID."), Microsoft.Xna.Framework.Color.Yellow);
                break;
            }
            if (account.UUID != player.UUID)
            {
                TShock.Log.ConsoleInfo(GetString($"[Chameleon] Player `{player.Name}` failed UUID auto-login. Password verification required."));
                break;
            }
            if (Configuration.Instance.VerifyloginIP &&
                JsonConvert.DeserializeObject<string[]>(account.KnownIps) is var knownIps &&
                (knownIps is null || player.IP != knownIps.Last()))
            {
                TShock.Log.ConsoleInfo(GetString($"[Chameleon] Player `{player.Name}`'s IP does not match the last login IP address. Password verification required."));
                break;
            }

            if (player.State == 1)
            {
                player.State = 2;
            }

            NetMessage.SendData((int) PacketTypes.WorldInfo, player.Index);

            player.PlayerData = TShock.CharacterDB.GetPlayerData(player, account.ID);
            var group = TShock.Groups.GetGroupByName(account.Group);

            player.Group = group;
            player.tempGroup = null;
            player.Account = account;
            player.IsLoggedIn = true;
            player.IsDisabledForSSC = false;

            if (Main.ServerSideCharacter)
            {
                if (player.HasPermission(Permissions.bypassssc))
                {
                    player.PlayerData.CopyCharacter(player);
                    TShock.CharacterDB.InsertPlayerData(player);
                }
                player.PlayerData.RestoreCharacter(player);
            }
            player.LoginFailsBySsi = false;

            if (player.HasPermission(Permissions.ignorestackhackdetection))
            {
                player.IsDisabledForStackDetection = false;
            }

            if (player.HasPermission(Permissions.usebanneditem))
            {
                player.IsDisabledForBannedWearable = false;
            }

            player.SendSuccessMessage(GetString($"[Chameleon] Verified `{account.Name}` login completed."));
            TShock.Log.ConsoleInfo(GetString($"[Chameleon] `{player.Name}` successfully verified login."));
            PlayerHooks.OnPlayerPostLogin(player);
            return true;
#pragma warning disable CS0162
        } while (false);
#pragma warning disable CS0162

        // Password Login part.2
        player.RequiresPassword = true;
        NetMessage.SendData((int) PacketTypes.PasswordRequired, player.Index);
        return true;
    }

    private static bool HandlePassword(TSPlayer player, string password)
    {
        var isRegister = player.GetData<bool>(WaitPwd4Reg);

        if (!player.RequiresPassword && !isRegister)
        {
            return true;
        }

        if (!isRegister && PlayerHooks.OnPlayerPreLogin(player, player.Name, password))
        {
            return true;
        }

        var account = TShock.UserAccounts.GetUserAccountByName(player.Name);
        if (account != null)
        {
            if (account.VerifyPassword(password))
            {
                player.RequiresPassword = false;
                player.PlayerData = TShock.CharacterDB.GetPlayerData(player, account.ID);

                if (player.State == 1)
                {
                    player.State = 2;
                }

                NetMessage.SendData((int) PacketTypes.WorldInfo, player.Index);

                var group = TShock.Groups.GetGroupByName(account.Group);

                player.Group = group;
                player.tempGroup = null;
                player.Account = account;
                player.IsLoggedIn = true;
                player.IsDisabledForSSC = false;

                if (Main.ServerSideCharacter)
                {
                    if (player.HasPermission(Permissions.bypassssc))
                    {
                        player.PlayerData.CopyCharacter(player);
                        TShock.CharacterDB.InsertPlayerData(player);
                    }
                    player.PlayerData.RestoreCharacter(player);
                }
                player.LoginFailsBySsi = false;

                if (player.HasPermission(Permissions.ignorestackhackdetection))
                {
                    player.IsDisabledForStackDetection = false;
                }

                if (player.HasPermission(Permissions.usebanneditem))
                {
                    player.IsDisabledForBannedWearable = false;
                }

                player.SendSuccessMessage(GetString($"[Chameleon] Verified `{account.Name}` login completed."));
                TShock.Log.ConsoleInfo(GetString($"[Chameleon] `{player.Name}` successfully verified login."));
                TShock.UserAccounts.SetUserAccountUUID(account, player.UUID);
                PlayerHooks.OnPlayerPostLogin(player);
                return true;
            }
            Kick(player, Configuration.Instance.VerificationFailedMessage, GetString("Incorrect Password..."));
            return true;
        }
        if (player.Name != TSServerPlayer.AccountName)
        {
            account = new UserAccount
            {
                Name = player.Name,
                Group = TShock.Config.Settings.DefaultRegistrationGroupName,
                UUID = player.UUID
            };
            try
            {
                account.CreateBCryptHash(password);
            }
            catch (ArgumentOutOfRangeException)
            {
                Kick(player, GetString($"Password must be at least {TShock.Config.Settings.MinimumPasswordLength} characters long."), GetString("Registration Failed"));
                return true;
            }
            player.SendSuccessMessage(GetString($"[Chameleon] Account `{account.Name}` registered successfully."));
            player.SendSuccessMessage(GetString($"[Chameleon] Your password is: [c/FF0000:{password}]"));
            TShock.UserAccounts.AddUserAccount(account);
            TShock.Log.ConsoleInfo(GetString($"[Chameleon] Player {player.Name} registered a new account: {account.Name}"));

            player.RequiresPassword = false;
            player.SetData(WaitPwd4Reg, false);
            player.PlayerData = TShock.CharacterDB.GetPlayerData(player, account.ID);

            if (player.State == 1)
            {
                player.State = 2;
            }

            NetMessage.SendData((int) PacketTypes.WorldInfo, player.Index);

            var group = TShock.Groups.GetGroupByName(account.Group);

            player.Group = group;
            player.tempGroup = null;
            player.Account = account;
            player.IsLoggedIn = true;
            player.IsDisabledForSSC = false;

            if (Main.ServerSideCharacter)
            {
                if (player.HasPermission(Permissions.bypassssc))
                {
                    player.PlayerData.CopyCharacter(player);
                    TShock.CharacterDB.InsertPlayerData(player);
                }
                player.PlayerData.RestoreCharacter(player);
            }
            player.LoginFailsBySsi = false;

            if (player.HasPermission(Permissions.ignorestackhackdetection))
            {
                player.IsDisabledForStackDetection = false;
            }

            if (player.HasPermission(Permissions.usebanneditem))
            {
                player.IsDisabledForBannedWearable = false;
            }

            player.SendSuccessMessage(GetString($"[Chameleon] Verified `{account.Name}` login completed."));
            TShock.Log.ConsoleInfo(GetString($"[Chameleon] `{player.Name}` successfully verified login."));
            TShock.UserAccounts.SetUserAccountUUID(account, player.UUID);
            PlayerHooks.OnPlayerPostLogin(player);
            return true;
        }

        // Reserved System Account Name
        Kick(player, GetString("This username is disabled."), GetString("Please change your character name"));
        return true;
    }

    private static void AddToList(string playerName)
    {
        PrepareList.Add(playerName);

        if (PrepareList.Count > 10)
        {
            PrepareList.RemoveAt(0);
        }
    }

    private static void Kick(TSPlayer player, string msg, string custom)
    {
        if (!player.ConnectionAlive)
        {
            return;
        }

        player.SilentKickInProgress = true;
        player.Disconnect($"{custom}: {msg}");
        TShock.Log.ConsoleInfo(GetString($"[Chameleon] Notification sent to `{player.Name}`."));
    }
}