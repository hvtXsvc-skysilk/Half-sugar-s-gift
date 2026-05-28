using hvtXsvc.Core;
using Nebula.Configuration;
using NebulaN.Roles.Modifier;
using GamePlayer = Virial.Game.Player;

namespace NebulaN.Roles.Neutral;

public class Imagination : DefinedRoleTemplate, HasCitation, DefinedRole,
    RuntimeAssignableGenerator<RuntimeRole>, IAssignableDocument
{
    public static readonly RoleTeam ImaginationTeam = NebulaAPI.Preprocessor.CreateTeam(
        "teams.imagination", new Virial.Color(128, 128, 128, byte.MaxValue), 0);

    public static GameEnd ImaginationWin { get; private set; } = null!;

    internal static IntegerConfiguration candidateCount = NebulaAPI.Configurations.Configuration(
        "options.role.imagination.candidateCount", (1, 10), 3, null, null);

    internal static SimpleRoleFilterConfiguration roleFilterOption = new SimpleRoleFilterConfiguration("options.role.imagination.roleFilter")
    {
        RolePredicate = (DefinedRole r) => true,
        ScrollerTag = "imaginationFilter",
        InvertOption = true,
        PreviewOnlySpawnableRoles = true
    };

    private Imagination() : base(
        "imagination",
        new Virial.Color(128, 128, 128, byte.MaxValue),
        RoleCategory.NeutralRole,
        ImaginationTeam,
        new Virial.Configuration.IConfiguration[] { candidateCount, roleFilterOption }
    )
    {
        ImaginationWin = NebulaAPI.Preprocessor!.CreateEnd("ImaginationWin", ImaginationTeam.Color, 0);
        ConfigurationHolder!.Illustration = NebulaAPI.AddonAsset
            .GetResource("BigPic/ImaginationPic.png")?.AsImage(115f);
    }

    Virial.Media.Image? DefinedAssignable.IconImage =>
        NebulaAPI.AddonAsset.GetResource("Smallicon/ImaginationIcon.png")?.AsImage();

    Citation? HasCitation.Citation => Citations.hvtXsvc_hsg;

    bool IAssignableDocument.HasTips => true;
    bool IAssignableDocument.HasAbility => true;

    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages()
    {
        yield return new AssignableDocumentImage(
            NebulaAPI.AddonAsset.GetResource("ImaginationSkill.png")?.AsImage(115f),
            "role.imagination.doc.skill"
        );
    }

    IEnumerable<AssignableDocumentReplacement> IAssignableDocument.GetDocumentReplacements()
    {
        yield return new AssignableDocumentReplacement("%NUM%", ((int)candidateCount).ToString());
    }

    public static readonly Imagination MyRole = new Imagination();

    public RuntimeRole CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);
    public class Instance : RuntimeAssignableTemplate, RuntimeRole, RuntimeAssignable, IGameOperator
    {
        void IGameOperator.OnReleased() { }
        public DefinedRole Role => MyRole;
        public Instance(GamePlayer player) : base(player) { }

        internal static HashSet<int> SelectedRoleIds = new();

        void RuntimeAssignable.OnActivated()
        {
            if (!AmOwner) return;
            if (!MyPlayer.TryGetModifier<ImaginationModifier.Instance>(out _))
                MyPlayer.AddModifier(ImaginationModifier.MyRole);
        }
    }
}