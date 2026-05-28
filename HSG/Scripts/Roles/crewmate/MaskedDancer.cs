using System;
using System.Collections.Generic;
using System.Linq;
using hvtXsvc.Core;
using Nebula.Game.Statistics;
using Nebula.Modules;
using Nebula.Modules.ScriptComponents;
using Nebula.Player; 
using Nebula.Utilities;
using NebulaN.Core;
using UnityEngine;
using Virial;
using Virial.Assignable;
using Virial.Compat;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;
using NPlayer = Virial.Game.Player;
using Vector3 = UnityEngine.Vector3;

namespace NebulaN.Scripts.Roles.crewmate;

public class MaskedDancer : DefinedRoleTemplate, HasCitation, DefinedRole,
    RuntimeAssignableGenerator<RuntimeRole>, IAssignableDocument
{
    private static Virial.Color LightBlue = new Virial.Color(0.5f, 0.8f, 1f);

    static private IntegerConfiguration partyUses = NebulaAPI.Configurations.Configuration(
        "options.role.maskeddancer.partyuses", (1, 10), 2, null, null);

    static private FloatConfiguration inviteCooldownA = NebulaAPI.Configurations.Configuration(
        "options.role.maskeddancer.inviteCooldownA", (0f, 60f, 2.5f), 10f,
        FloatConfigurationDecorator.Second, null, null);
    static private FloatConfiguration inviteCooldownB = NebulaAPI.Configurations.Configuration(
        "options.role.maskeddancer.inviteCooldownB", (0f, 60f, 2.5f), 15f,
        FloatConfigurationDecorator.Second, null, null);
    static private FloatConfiguration inviteCooldownC = NebulaAPI.Configurations.Configuration(
        "options.role.maskeddancer.inviteCooldownC", (0f, 60f, 2.5f), 20f,
        FloatConfigurationDecorator.Second, null, null);
    static private FloatConfiguration startPartyCooldown = NebulaAPI.Configurations.Configuration(
        "options.role.maskeddancer.startcooldown", (10f, 120f, 5f), 30f,
        FloatConfigurationDecorator.Second, null, null);

    private MaskedDancer() : base(
        "maskedDancer", LightBlue, RoleCategory.CrewmateRole, NebulaTeams.CrewmateTeam,
        new Virial.Configuration.IConfiguration[] {
            partyUses, inviteCooldownA, inviteCooldownB, inviteCooldownC, startPartyCooldown
        })
    {
       ConfigurationHolder!.Illustration = NebulaAPI.AddonAsset.GetResource("BigPic/MaskedDancer.png")?.AsImage(115f);
    }

    Virial.Media.Image? DefinedAssignable.IconImage => NebulaAPI.AddonAsset.GetResource("Smallicon/MaskedDancerIcon.png")?.AsImage();

    Citation? HasCitation.Citation => Citations.hvtXsvc_hsg;

    bool IAssignableDocument.HasTips => true;
    bool IAssignableDocument.HasAbility => true;

    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages()
    {
        yield return new AssignableDocumentImage(
            NebulaAPI.AddonAsset.GetResource("MaskedDancerInvite.png")?.AsImage(100f),
            "role.maskeddancer.doc.invite");
        yield return new AssignableDocumentImage(
            NebulaAPI.AddonAsset.GetResource("MaskedDancerStart.png")?.AsImage(100f),
            "role.maskeddancer.doc.start");
    }

    IEnumerable<AssignableDocumentReplacement> IAssignableDocument.GetDocumentReplacements()
    {
        yield return new AssignableDocumentReplacement("%USES%", ((int)partyUses).ToString());
    }

    public static readonly MaskedDancer MyRole = new MaskedDancer();

    public RuntimeRole CreateInstance(NPlayer player, int[] arguments) => new Instance(player);

    public class Instance : RuntimeAssignableTemplate, RuntimeRole, RuntimeAssignable, IGameOperator
    {
        void IGameOperator.OnReleased() { }
        IEnumerable<IPlayerAbility> RuntimeAssignable.MyAbilities => Array.Empty<IPlayerAbility>();

        int usesLeft;
        List<NPlayer> invitedPlayers = new();
        bool PartyCanUse = false;

        List<PoolablePlayer> invitedIcons = new();
        Dictionary<byte, PoolablePlayer> iconDict = new();
        GameObject? iconHolder;

        public Instance(NPlayer player) : base(player) { }
        public DefinedRole Role => MyRole;
        private void UpdateIconsLayout()
        {
            for (int i = 0; i < invitedIcons.Count; i++)
            {
                var icon = invitedIcons[i];
                if (icon){icon.transform.localPosition = new Vector3(-0.5f + i * 0.35f, 0f, 0f); }
            }
        }

        void RuntimeAssignable.OnActivated()
        {
            if (!AmOwner) return;

            usesLeft = partyUses;
            invitedPlayers.Clear();
            PartyCanUse = false;
            iconHolder = HudContent.InstantiateContent("MaskedDancerIcons", true, true).gameObject;
            invitedIcons.Clear();
            iconDict.Clear();

            Virial.Media.Image inviteIcon = NebulaAPI.AddonAsset.GetResource("MaskedDancerInvite.png")?.AsImage(100f);
            Virial.Media.Image startIcon = NebulaAPI.AddonAsset.GetResource("MaskedDancerStart.png")?.AsImage(100f);

            var playerTracker = NebulaAPI.Modules.PlayerTracker(this, MyPlayer);
            playerTracker.SetColor(MyRole.RoleColor);
            var invite = NebulaAPI.Modules.AbilityButton(
                this, MyPlayer, VirtualKeyInput.Ability, 0f,
                "maskedDancer.invite", inviteIcon,
                (ModAbilityButton _) => playerTracker.CurrentTarget != null
                    && playerTracker.CurrentTarget != MyPlayer
                    && !invitedPlayers.Contains(playerTracker.CurrentTarget)
                    && invitedPlayers.Count < 3,
                (button) => !MyPlayer.IsDead && usesLeft > 0,
                false
            );

            invite.OnClick = (button) =>
            {
                var targetLike = playerTracker.CurrentTarget;
                if (targetLike == null) return;
                var invitedPlayer = targetLike.RealPlayer;
                if (invitedPlayers.Contains(invitedPlayer)) return;

                invitedPlayers.Add(invitedPlayer);
                PlayerControl pc = null;
                foreach (var p in PlayerControl.AllPlayerControls)
                {
                    if (p.PlayerId == invitedPlayer.PlayerId)
                    {
                        pc = p;
                        break;
                    }
                }
                if (pc != null && iconHolder != null)
                {
                    var icon = AmongUsUtil.GetPlayerIcon(
                        pc.Data.DefaultOutfit, 
                        iconHolder.transform,
                        Vector3.zero,
                        Vector3.one * 0.31f,
                        false, true
                    );
                    icon.ToggleName(false);
                    icon.SetAlpha(0.5f);
                    int i=invitedPlayers.Count - 1;
                    icon.transform.localPosition = new Vector3(i*0.29f-0.3f,-0.1f,-i*0.01f);
                    invitedIcons.Add(icon);
                    iconDict[invitedPlayer.PlayerId] = icon;
                }
                 button.StartCoolDown();

                float nextCd = invitedPlayers.Count switch
                {
                    1 => inviteCooldownA,
                    2 => inviteCooldownB,
                    3 => inviteCooldownC,
                    _ => 10f
                };
                button.CoolDownTimer = NebulaAPI.Modules.Timer(this, nextCd).SetAsAbilityTimer().Start(nextCd);
                UpdateIconsLayout();
            };
            var startBtn = NebulaAPI.Modules.AbilityButton(
                this, MyPlayer, VirtualKeyInput.SecondaryAbility,
                startPartyCooldown, "maskedDancer.start", startIcon,
                (ModAbilityButton _) => invitedPlayers.Count == 3 && PartyCanUse && !MyPlayer.IsDead,
                (button) => !MyPlayer.IsDead && usesLeft > 0,
                false
            );

            startBtn.ShowUsesIcon(4, usesLeft.ToString());

            startBtn.OnClick = (button) =>
            {
                if (invitedPlayers.Count != 3 || !PartyCanUse) return;

                int a = 0, b = 0, c = 0;
                foreach (var p in invitedPlayers)
                {
                    switch (p.Role.Role.Category)
                    {
                        case RoleCategory.CrewmateRole: a++; break;
                        case RoleCategory.ImpostorRole: b++; break;
                        case RoleCategory.NeutralRole: c++; break;// 吓哭了c++。（？
                    }
                }

                if (a == 1 && b == 1 && c == 1) { /*？！棍母发生了！？*/}
                else if (a == 3) {/*喵。*/}
                else if (a == 2)
                {
                    foreach (var p in invitedPlayers)
                    {
                        if (p.Role.Role.Category != RoleCategory.CrewmateRole)
                        {
                            p.Suicide(State.PartyAccident, null, KillParameter.NormalKill, null);
                            break;
                        }
                    }
                }
                else if (a == 0)
                {
                    MyPlayer.Suicide(State.PartyAccident, null, KillParameter.NormalKill, null);
                }
                else
                {
                    var target = invitedPlayers[UnityEngine.Random.Range(0, invitedPlayers.Count)];
                    target.Suicide(State.PartyAccident, null, KillParameter.NormalKill, null);
                }
                foreach (var icon in invitedIcons)
                    if (icon) GameObject.Destroy(icon.gameObject);
                invitedIcons.Clear();
                iconDict.Clear();
                invitedPlayers.Clear();
                PartyCanUse = false;
                usesLeft--;
                button.UpdateUsesIcon(usesLeft.ToString());
            };

            GameOperatorManager.Instance?.Subscribe<MeetingEndEvent>(ev =>
            {
                if (AmOwner && !MyPlayer.IsDead && invitedPlayers.Count == 3)
                    PartyCanUse = true;

                var deadList = invitedPlayers.Where(p => p.IsDead).ToList();
                foreach (var dead in deadList)
                {
                    if (iconDict.TryGetValue(dead.PlayerId, out var icon))
                    {
                        if (icon) GameObject.Destroy(icon.gameObject);
                        iconDict.Remove(dead.PlayerId);
                        invitedIcons.Remove(icon);
                    }
                    invitedPlayers.Remove(dead);
                }
                UpdateIconsLayout();
            }, this);
        }


    }
}