using hvtXsvc.Core;
using Nebula.Modules;
using Nebula.Modules.ScriptComponents;
using Nebula.Player;
using Nebula.Utilities;
using NebulaN.Core;
using NebulaN.Roles.Modifier;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;
using GamePlayer = Virial.Game.Player;

namespace NebulaN.Roles.Crewmate;

public class Referee : DefinedRoleTemplate, HasCitation, DefinedRole,
    RuntimeAssignableGenerator<RuntimeRole>, IAssignableDocument
{

    static private FloatConfiguration recruitCooldown = NebulaAPI.Configurations.Configuration(
        "options.role.referee.cooldown",
        (5f, 60f, 2.5f),
        20f,
        FloatConfigurationDecorator.Second,
        null, null
    );

    static private BoolConfiguration suicideOnImpostor = NebulaAPI.Configurations.Configuration(
        "options.role.referee.suicideImpostor",
        true,
        null, null
    );

    static private BoolConfiguration suicideOnNeutral = NebulaAPI.Configurations.Configuration(
        "options.role.referee.suicideNeutral",
        false,
        null, null
    );

    static private BoolConfiguration suicideOnCrewmate = NebulaAPI.Configurations.Configuration(
        "options.role.referee.suicideCrewmate",
        false,
        null, null
    );

    private const int MaxUse = 1;

    private Referee() : base(
        "referee",
        Cor.cyan,
        RoleCategory.CrewmateRole,
        NebulaTeams.CrewmateTeam,
        new Virial.Configuration.IConfiguration[] {
            recruitCooldown,
            suicideOnImpostor,
            suicideOnNeutral,
            suicideOnCrewmate,
            Patch.RefereeChatEnabled
        }
    )
    {
        ConfigurationHolder!.Illustration = NebulaAPI.AddonAsset.GetResource("BigPic/RefereePic.png")?.AsImage(115f);
    }

    // Virial.Media.Image? DefinedAssignable.IconImage => NebulaAPI.AddonAsset.GetResource("Smallicon/RefereeIcon.png")?.AsImage();

    Citation? HasCitation.Citation => Citations.hvtXsvc_hsg;

    bool IAssignableDocument.HasTips => true;
    bool IAssignableDocument.HasAbility => true;

    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages()
    {
        yield return new AssignableDocumentImage(
            NebulaAPI.AddonAsset.GetResource("RefereeRecruit.png")?.AsImage(115f),
            "role.referee.doc.recruit"
        );
    }

    IEnumerable<AssignableDocumentReplacement> IAssignableDocument.GetDocumentReplacements()
    {
        yield return new AssignableDocumentReplacement("%USES%", MaxUse.ToString());
    }

    public static readonly Referee MyRole = new Referee();

    public RuntimeRole CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    public class Instance : RuntimeAssignableTemplate, RuntimeRole, RuntimeAssignable, IGameOperator
    {
        void IGameOperator.OnReleased() { }
        IEnumerable<IPlayerAbility> RuntimeAssignable.MyAbilities => Array.Empty<IPlayerAbility>();
        public DefinedRole Role => MyRole;
        public Instance(GamePlayer player) : base(player) { }

        private int usesLeft;
        private SpriteRenderer? lockSprite;
        private ModAbilityButton? recruitBtn;
        private bool firstMeetingOccurred = false;

        void RuntimeAssignable.OnActivated()
        {
            if (!AmOwner) return;
            usesLeft = MaxUse;
            firstMeetingOccurred = false;
            GameOperatorManager.Instance?.Subscribe<MeetingEndEvent>(ev =>
            {
                firstMeetingOccurred = true;
                if (lockSprite)
                {
                    GameObject.Destroy(lockSprite.gameObject);
                    lockSprite = null;
                }
            }, this);
            Virial.Media.Image recruitIcon = NebulaAPI.AddonAsset.GetResource("RefereeRecruit.png")?.AsImage(100f);
            var playerTracker = NebulaAPI.Modules.PlayerTracker(this, MyPlayer);
            playerTracker.SetColor(MyRole.RoleColor);

            recruitBtn = NebulaAPI.Modules.AbilityButton(
                this, MyPlayer,
                VirtualKeyInput.Ability,
                recruitCooldown,
                "referee.recruit",
                recruitIcon,
                (ModAbilityButton _) => playerTracker.CurrentTarget != null
                    && playerTracker.CurrentTarget != MyPlayer
                    && firstMeetingOccurred,
                (button) => !MyPlayer.IsDead && usesLeft > 0,
                false
            );

            recruitBtn.ShowUsesIcon(4, usesLeft.ToString());
            if (!firstMeetingOccurred)
            {
                lockSprite = (recruitBtn as ModAbilityButtonImpl)?.VanillaButton.AddLockedOverlay();
            }

            recruitBtn.OnClick = (button) =>
            {
                var target = playerTracker.CurrentTarget;
                if (target == null || target == MyPlayer || usesLeft <= 0) return;

                var originalCategory = target.Role.Role.Category;
                bool shouldSuicide = false;
                if (originalCategory == RoleCategory.ImpostorRole && suicideOnImpostor) shouldSuicide = true;
                else if (originalCategory == RoleCategory.NeutralRole && suicideOnNeutral) shouldSuicide = true;
                else if (originalCategory == RoleCategory.CrewmateRole && suicideOnCrewmate) shouldSuicide = true;

                if (shouldSuicide)
                {
                    MyPlayer.Suicide(PlayerState.Dead, null, KillParameter.NormalKill, null);
                    return;
                }
                var justiceRole = Nebula.Roles.Roles.AllRoles.FirstOrDefault(r => r.InternalName == "justice");
                if (justiceRole == null) return;
                target.SetRole(justiceRole, justiceRole.DefaultAssignableArguments);
                target.AddModifier(RefereeRecruited.MyRole);
                usesLeft--;
                button.UpdateUsesIcon(usesLeft.ToString());
                button.StartCoolDown();
            };
        }
    }
}