using Nebula.Utilities;
using Virial;
using Virial.Assignable;
using Virial.Text;
using Color = Virial.Color;

namespace hvtXsvc.Core
{

    public static class Citations
    {

        public static Citation hvtXsvc_hsg { get; private set; } = new(
            "Half Sugars Gift",
            NebulaAPI.AddonAsset.GetResource("Citat/HalfSugarGift.png")?.AsImage(125f),
            new ColorTextComponent(
                new UnityEngine.Color(1f, 0f, 1f),                   
                new RawTextComponent("Half Sugars Gift")
            ),
            "Citat/HalfSugarGift.png"
        );
        public static Citation AmongUs { get; private set; } = new(
            "Innersloth",
            NebulaAPI.AddonAsset.GetResource("Citat/AmongUs.png")?.AsImage(125f),
            new ColorTextComponent(
                new UnityEngine.Color(1f, 1f, 1f),
                new RawTextComponent("Innersloth")
            ),
            "https://www.innersloth.com/games/among-us/"
        );
        public static Citation TheOtherRoles { get; private set; } = new(
            "The Other Roles",
            NebulaAPI.AddonAsset.GetResource("Citat/TOR_logo.png")?.AsImage(125f),
            new ColorTextComponent(
                new UnityEngine.Color(1f, 0f, 0f),
                new RawTextComponent("The Other Roles")
            ),
            "https://github.com/TheOtherRolesAU/TheOtherRoles"
        );
    }
}