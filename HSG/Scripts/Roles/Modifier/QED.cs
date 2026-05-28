using Virial;
using Virial.Assignable;
using Virial.Game;
using hvtXsvc.Core;
using GamePlayer = Virial.Game.Player;

namespace NebulaN.Roles.Modifier;

public class QED : DefinedAllocatableModifierTemplate, HasCitation, DefinedAllocatableModifier
{
    private static readonly FloatConfiguration Cooldown = NebulaAPI.Configurations.Configuration(
        "options.modifier.qed.cooldown",
        (0f, 60f, 2.5f),
        3f,
        FloatConfigurationDecorator.Second
    );

    private QED() : base(
        "QED",
        "QED",
        new Virial.Color(0.15f, 0f, 0.85f),
        new Virial.Configuration.IConfiguration[] { Cooldown },
        allocateToCrewmate: true,
        allocateToImpostor: true,
        allocateToNeutral: true
    )
    { }

    public static readonly QED MyRole = new();
    public Citation Citation => Citations.hvtXsvc_hsg;
    RuntimeModifier RuntimeAssignableGenerator<RuntimeModifier>.CreateInstance(GamePlayer player, int[] arguments)
        => new Instance(player);

    public class Instance : RuntimeAssignableTemplate, RuntimeModifier
    {
        DefinedModifier RuntimeModifier.Modifier => MyRole;

        private readonly Virial.Media.Image? _buttonImage = NebulaAPI.AddonAsset.GetResource("KeyMasterButton.png")?.AsImage(115f);
        private ModAbilityButton? _abilityButton;

        public Instance(GamePlayer player) : base(player) { }

        void RuntimeAssignable.OnActivated()
        {
            if (!AmOwner) return;
            _abilityButton = NebulaAPI.Modules.AbilityButton(
                lifespan: this,
                player: MyPlayer,
                input: VirtualKeyInput.None,
                cooldown: Cooldown,
                label: "QED.cnj",
                image: _buttonImage
            );
            _abilityButton.SetLabelType(ModAbilityButton.LabelType.Standard);
            _abilityButton.Visibility = _ => !MyPlayer.IsDead;
            _abilityButton.Availability = _ => !MyPlayer.IsDead;

            _abilityButton.OnClick = _ =>
            {
                SoundManagers.RpcPlayPositional("QED", MyPlayer.Position, 0.75f);
            };
        }
        void RuntimeAssignable.DecorateNameConstantly(ref string name, bool canSeeAllInfo, bool inEndScene)
        {
            if (AmOwner)
                name += " QED".Color(new UnityEngine.Color(0.15f, 0f, 0.85f));
        }
    }
}