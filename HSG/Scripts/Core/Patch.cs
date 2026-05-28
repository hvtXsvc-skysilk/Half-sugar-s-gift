using HarmonyLib;
using InnerNet;
using Nebula.Modules;
using Nebula.Utilities;
using NebulaN.Roles.Impostor;
using NebulaN.Roles.Modifier;
using NebulaN.Roles.Neutral;
using System;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using Virial.Attributes;
using Virial.Events.Game;
using Virial.Game;
using Virial.Runtime;
using GamePlayer = Virial.Game.Player;

namespace NebulaN.Core;

static public class Cor
{
    static public Virial.Color cyan = new(0f, 1f, 1f);
    static public Virial.Color impRed = new(Palette.ImpostorRed.r,Palette.ImpostorRed.g,Palette.ImpostorRed.b);
    static public Virial.Color lightYellow = new(1f, 0.9f, 0.6f);
    static public Virial.Color green = new(0f,1f,0f);
    static public Virial.Color blue = new(0f,0f,1f);
    static public Virial.Color White=new(1f,1f,1f);
    static public Virial.Color SpiritCor=new(0f,0.1f,0.4f);
}
public class State
{
    /// <summary>
    /// 死因：碎望
    /// </summary>
    public static TranslatableTag BrokenWish = new TranslatableTag("state.brokewish");
    /// <summary>
    /// 死因：抑郁
    /// </summary>
    public static TranslatableTag Depression = new TranslatableTag("state.imaginationDepression");
    /// <summary>
    /// 死因：舞会事故
    /// </summary>
    public static TranslatableTag PartyAccident = new TranslatableTag("state.partyAccident");
    /// <summary>
    /// 死因：散灵
    /// </summary>
    public static TranslatableTag SanLing = new TranslatableTag("state.sanling");
    /// <summary>
    /// 死因：魂归
    /// </summary>
    public static TranslatableTag SoulBack = new TranslatableTag("state.soulback"); // 本来我想叫这个state.sb。
}
public static class team
{
    /// <summary>
    /// 灵阵营
    /// </summary>
    public static readonly RoleTeam SpiritTeam = NebulaAPI.Preprocessor.CreateTeam("teams.spirit", new Virial.Color(0f, 0.1f, 0.4f), 0);
    public static readonly GameEnd SpiritWin = NebulaAPI.Preprocessor.CreateEnd("spiritWin", SpiritTeam.Color, 71);
    /// <summary>
    /// 寻仇者阵营
    /// </summary>
    public static readonly RoleTeam HAvengerTeam = NebulaAPI.Preprocessor.CreateTeam("teams.Havenger", new Virial.Color(0.8f, 0.2f, 0.2f), TeamRevealType.OnlyMe);
    public static readonly GameEnd HAvengerWin = NebulaAPI.Preprocessor.CreateEnd("HavengerWin", new Virial.Color(0.8f, 0.2f, 0.2f), 70);
}


[NebulaPreprocess(PreprocessPhase.PostFixStructure)]
[NebulaRPCHolder]
public static class Patch
{
    public static bool IsAdmin(PlayerControl player)
    {
        return AmongUsClient.Instance.AmHost;
    }
    public static BoolConfiguration RefereeChatEnabled = NebulaAPI.Configurations.Configuration("options.referee.chatEnabled", true, null, null);
    private static void SendLocalMessage(string message)
    {
        var player = PlayerControl.LocalPlayer;
        string originalName = player.name;
        player.SetName("<b>HalfSugarGift/<b>");
        DestroyableSingleton<HudManager>.Instance.Chat.AddChat(player, message);
        player.SetName(originalName);
    }
    private static bool IsRefereeOrJustice(GamePlayer player)
    {
        var role = player.Role.Role;
        if (role.InternalName == "referee") return true;
        if (role.InternalName == "justice" && player.TryGetModifier<RefereeRecruited.Instance>(out _))
            return true;
        return false;
    }
    public static void Preprocess(NebulaPreprocessor preprocessor)
    {
        var harmony = new Harmony("Hsg.addon.commands");
        var original = typeof(ChatController).GetMethod("SendChat");
        var prefix = new HarmonyMethod(typeof(Patch).GetMethod(nameof(OnSendChat), BindingFlags.Static | BindingFlags.NonPublic));
        HsgDebug.Log("loading private chat");
        if (original != null)
        {
            harmony.Patch(original, prefix: prefix);
            HsgDebug.Log("成功。");
        }
        else
        {
            HsgDebug.Log("失败。");
        }
    }
    private static bool OnSendChat(ChatController __instance)
    {
        string text = __instance.freeChatField.textArea.text.Trim();
        string[] strs = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (strs.Length == 0) return true;

        string cmd = strs[0].ToLower();
        bool inLobby = LobbyBehaviour.Instance != null;
        bool isAdmin = IsAdmin(PlayerControl.LocalPlayer);
        if (cmd == "/ghelp")
        {
            ShowHelp(__instance);
            return false;
        }
        if (cmd == "/suicide" || cmd == "/killself" || cmd == "/zs")
        {
            if (inLobby || !isAdmin)
            {
                __instance.freeChatField.Clear();
                return false;
            }
            SuicideCommand(__instance);
            return false;
        }
        if (cmd == "/kill")
        {
            if (inLobby || !isAdmin)
            {
                __instance.freeChatField.Clear();
                return false;
            }
            KillCommand(__instance, strs);
            return false;
        }
        if (cmd == "/changerole" || cmd == "/cr" || cmd == "/bzy")
        {
            if (inLobby || !isAdmin)
            {
                __instance.freeChatField.Clear();
                return false;
            }
            ChangeRoleCommand(__instance, strs);
            return false;
        }
        if (cmd == "/sus" || cmd == "/suspect")
        {
            if (inLobby || !isAdmin)
            {
                __instance.freeChatField.Clear();
                return false;
            }
            SuspectCommand(__instance, strs);
            return false;
        }
        if (cmd == "/return" || cmd == "/quit" || cmd == "/kickself")
        {
            if (IsAdmin(PlayerControl.LocalPlayer))
            {
                __instance.freeChatField.Clear();
                return false;
            }
            string reason = strs.Length > 1 ? string.Join(" ", strs.Skip(1)) : null;
            RpcReturnRequest.Invoke((PlayerControl.LocalPlayer.PlayerId, reason));
            __instance.freeChatField.Clear();
            return false;
        }
        if (cmd == "/rechat")
        {
            if (!RefereeChatEnabled)
            {
                __instance.freeChatField.Clear();
                return false;
            }

            var localPlayer = GamePlayer.GetPlayer(PlayerControl.LocalPlayer.PlayerId);
            if (localPlayer == null)
            {
                __instance.freeChatField.Clear();
                return false;
            }

            if (!IsRefereeOrJustice(localPlayer))
            {
                __instance.freeChatField.Clear();
                return false;
            }

            if (strs.Length < 2)
            {
                __instance.freeChatField.Clear();
                return false;
            }
            string message = string.Join(" ", strs.Skip(1));
            RpcReChat.Invoke((PlayerControl.LocalPlayer.PlayerId, message));
            __instance.freeChatField.Clear();
            return false;
        }
        if (cmd == "/owlchat")
        {
            var localPlayer = GamePlayer.GetPlayer(PlayerControl.LocalPlayer.PlayerId);
            if (localPlayer == null || !Owl.CanUseOwlChat(localPlayer))
            {
                __instance.freeChatField.Clear();
                return false;
            }
            if (strs.Length < 2)
            {
                __instance.freeChatField.Clear();
                return false;
            }
            string message = string.Join(" ", strs.Skip(1));
            OwlChatHelper.SendOwlChat(message);
            __instance.freeChatField.Clear();
            return false;
        }

        return true;
    }
    private static void ShowHelp(ChatController __instance)
    {
        __instance.freeChatField.Clear();
        var sb = new StringBuilder();
        sb.AppendLine("<b><>HalfSugarGift指令帮助。</b></b>");
        sb.AppendLine("====================");
        sb.AppendLine("<b>/Ghelp</b>  — 显示本帮助消息");
        sb.AppendLine("<b>/suicide</b> (/killself /zs) — 自杀");
        sb.AppendLine("<b>/kill</b> <玩家名> — 击杀指定玩家");
        sb.AppendLine("<b>/changerole</b> <玩家名> <职业名> (/cr /bzy) — 改变某人的职业");
        sb.AppendLine("<b>/return</b>  <理由> (/quit /kickself) — 踢出自己");
        sb.AppendLine("====================");
        SendLocalMessage(sb.ToString());
    }
    public static ClientData? GetClient(PlayerControl player)
    {
        try
        {
            return AmongUsClient.Instance.allClients.ToArray()
                .FirstOrDefault(cd => cd.Character.PlayerId == player.PlayerId);
        }
        catch { return null; }
    }
    private static void SuicideCommand(ChatController __instance)
    {
        __instance.freeChatField.Clear();
        PlayerControl.LocalPlayer.RpcMurderPlayer(PlayerControl.LocalPlayer, true);
    }

    private static void KillCommand(ChatController __instance, string[] args)
    {
        __instance.freeChatField.Clear();
        if (args.Length < 2) return;

        string targetName = args[1];
        var target = PlayerControl.AllPlayerControls.ToArray()
            .FirstOrDefault(p => p.Data.PlayerName.Contains(targetName, StringComparison.OrdinalIgnoreCase));
        if (target == null) return;

        PlayerControl.LocalPlayer.RpcMurderPlayer(target, true);
    }

    private static void ChangeRoleCommand(ChatController __instance, string[] args)
    {
        __instance.freeChatField.Clear();
        if (args.Length < 3) return;

        string targetName = args[1];
        string roleName = string.Join(" ", args.Skip(2));

        var target = PlayerControl.AllPlayerControls.ToArray()
            .FirstOrDefault(p => p.Data.PlayerName.Contains(targetName, StringComparison.OrdinalIgnoreCase));
        if (target == null) return;

        var role = Nebula.Roles.Roles.AllRoles.FirstOrDefault(r =>
            r.InternalName.Equals(roleName, StringComparison.OrdinalIgnoreCase) ||
            r.LocalizedName.Equals(roleName, StringComparison.OrdinalIgnoreCase));
        if (role == null) return;

        var gamePlayer = GamePlayer.GetPlayer(target.PlayerId);
        if (gamePlayer == null) return;

        gamePlayer.SetRole(role, role.DefaultAssignableArguments);
        SendLocalMessage("更改成功");
    }

    private static RemoteProcess<(byte playerId, string? reason)> RpcReturnRequest = new(
    "ReturnRequest",
    (data, _) =>
    {
        if (!AmongUsClient.Instance.AmHost) return;

        var target = PlayerControl.AllPlayerControls.ToArray()
            .FirstOrDefault(p => p.PlayerId == data.playerId);
        if (target == null) return;
        string playerName = target.Data.PlayerName;
        string message = $"<color=cyan>[HalfSugarGift]</color> {playerName}离开了房间";
        if (!string.IsNullOrEmpty(data.reason))
            message += $" Reason：{data.reason}";
        PlayerControl.LocalPlayer.RpcSendChat(message);
        AmongUsClient.Instance.KickPlayer(GetClient(target)!.Id, false);
    });
    private static void SuspectCommand(ChatController __instance, string[] args)
    {
        __instance.freeChatField.Clear();
        if (args.Length < 2) return;

        string targetName = args[1];
        var target = PlayerControl.AllPlayerControls.ToArray()
            .FirstOrDefault(p => p.Data.PlayerName.Contains(targetName, StringComparison.OrdinalIgnoreCase));
        if (target == null) return;

        var gamePlayer = GamePlayer.GetPlayer(target.PlayerId);
        if (gamePlayer == null) return;

        var role = gamePlayer.Role;
        PlayerControl.LocalPlayer.RpcSendChat($"<color=yellow>[侦探]</color> {target.Data.PlayerName} 的职业是：{role.DisplayColoredName}");
    }
    private static RemoteProcess<(byte, string)> RpcReChat = new(
        "RefereeReChat",
        (data, _) =>
        {
            var localPlayer = GamePlayer.GetPlayer(PlayerControl.LocalPlayer.PlayerId);
            if (localPlayer == null) return;
            if (!IsRefereeOrJustice(localPlayer)) return;

            var sourcePlayer = GamePlayer.GetPlayer(data.Item1);
            if (sourcePlayer == null) return;

            var srcRole = sourcePlayer.Role.Role;
            if (srcRole == null) return;

            var pc = PlayerControl.AllPlayerControls.ToArray()
                .FirstOrDefault(p => p.PlayerId == sourcePlayer.PlayerId);
            if (pc == null) return;

            string origName = pc.name;
            pc.SetName(origName + " (" + srcRole.DisplayColoredName + ")");
            DestroyableSingleton<HudManager>.Instance.Chat.AddChat(pc, data.Item2);
            pc.SetName(origName);
        });
}