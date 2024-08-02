using Virial.Attributes;
using Virial.Compat;
using Virial.Media;
using Virial;
using Virial.Assignable;
using Nebula.Roles;
using Nebula.Player;
using System.Collections.Generic;

namespace DefaultLang.Documents;

[AddonDocument("role.cleaner")]
public class CleanerDocument : IDocument
{
    Virial.Media.GUIWidget? IDocument.Build(Artifact<GUIScreen>? target)
    {
        var gui = NebulaAPI.GUI;
        var syncCoolDown = RoleDocumentHelper.ConfigBool("options.role.cleaner.syncKillAndCleanCoolDown", "role.cleaner.ability.main.cooldown");
        return RoleDocumentHelper.GetRoleWidget("cleaner",
            RoleDocumentHelper.GetChapter("role.cleaner.ability", [
                RoleDocumentHelper.GetImageLocalizedContent("Buttons.CleanButton.png", "role.cleaner.ability.main", t => t.Replace("#COOLDOWN", syncCoolDown))
                ]),
            RoleDocumentHelper.GetConfigurationCaption()
            );
    }
}

[AddonDocument("role.destroyer")]
public class DestroyerDocument : IDocument
{
    Virial.Media.GUIWidget? IDocument.Build(Artifact<GUIScreen>? target)
    {
        var gui = NebulaAPI.GUI;
        var leftEvidence = RoleDocumentHelper.ConfigBool("options.role.destroyer.leaveKillEvidence", "role.destroyer.ability.main.leftEvidence");
        return RoleDocumentHelper.GetRoleWidget("destroyer",
            RoleDocumentHelper.GetChapter("role.destroyer.ability", [
                RoleDocumentHelper.GetDocumentLocalizedText("role.destroyer.ability.main", t => t.Replace("#LEFTEVIDENCE", leftEvidence))
                ]),
            RoleDocumentHelper.GetTipsChapter("role.destroyer"),
            RoleDocumentHelper.GetConfigurationCaption()
            );
    }
}

[AddonDocument("role.jailer", false)]
[AddonDocument("role.jailerModifier", true)]
public class JailerDocument : IDocument
{
    bool isModifier;
    public JailerDocument(bool isModifier) { this.isModifier = isModifier; }
    Virial.Media.GUIWidget? IDocument.Build(Artifact<GUIScreen>? target)
    {
        var gui = NebulaAPI.GUI;
        var text = RoleDocumentHelper.ConfigBool("options.role.jailer.canMoveWithMapWatching", "role.jailer.ability.main.canWalk", "role.jailer.ability.main.cannotWalk");
        text = text
        .Replace("#DEADBODY", RoleDocumentHelper.ConfigBool("options.role.jailer.canIdentifyDeadBodies", "role.jailer.ability.main.deadbody"))
        .Replace("#IMPOSTOR", RoleDocumentHelper.ConfigBool("options.role.jailer.canIdentifyImpostors", "role.jailer.ability.main.impostor"));
        if (isModifier) text = text.Replace("#INHERIT", "");
        else text = text.Replace("#INHERIT", RoleDocumentHelper.ConfigBool("options.role.jailer.inheritAbilityOnDying", "role.jailer.ability.main.inherit"));

        DefinedAssignable role = NebulaAPI.GetRole("jailer")!;
        DefinedAssignable assignable = isModifier ? NebulaAPI.GetModifier("jailerModifier")! : role;
        return NebulaAPI.GUI.VerticalHolder(GUIAlignment.Left,[
            RoleDocumentHelper.GetAssignableNameWidget(assignable),
            RoleDocumentHelper.GetConfigurationsChapter(assignable),
            RoleDocumentHelper.GetChapter("role.jailer.ability", [
                RoleDocumentHelper.GetDocumentText(text)
                ]),
            RoleDocumentHelper.GetTipsChapter("role.jailer"),
            RoleDocumentHelper.GetConfigurationCaption(),
            RoleDocumentHelper.GetAchievementWidget(role)]
            );
    }
}

[AddonDocument("role.sniper")]
public class SniperDocument : AbstractAssignableDocument
{
    public override GUIWidget? GetTipsWidget()
    {
        var aimAssistDelay = RoleDocumentHelper.Config<float>("options.role.sniper.delayInAimAssistActivation");
        var sound = RoleDocumentHelper.Config<float>("options.role.sniper.shotNoticeRange");
        var aimAssist = RoleDocumentHelper.ConfigBool("options.role.sniper.aimAssist", "role.sniper.tips.aimAssist").Replace("#DELAY", aimAssistDelay);
        return RoleDocumentHelper.GetDocumentLocalizedText("role.sniper.tips", t => t.Replace("#AIMASSIST", aimAssist).Replace("#SOUND", sound));
    }
    public override IEnumerable<GUIWidget> GetAbilityWidget() {
        yield return RoleDocumentHelper.GetImageLocalizedContent("Buttons.SnipeButton.png", "role.sniper.ability.snipe");
    }
    public override RoleType RoleType => RoleType.Role;
}