using Innersloth.IO;
using Microsoft.VisualBasic;
using Mono.CSharp;
using Nebula.Commands.Variations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Commands;

[NebulaPreLoad(typeof(NebulaResourceManager))]
static public class Commands
{
    static public void Load()
    {
        CommandManager.RegisterCommand(new FormulaCommand(), "nebula::formula", "nebula::f");
        CommandManager.RegisterCommand(new IfCommand(), "nebula::if");
        CommandManager.RegisterCommand(new LetCommand(), "nebula::let");
        CommandManager.RegisterCommand(new LetsCommand(), "nebula::lets", "nebula::scope");
        CommandManager.RegisterCommand(new EchoCommand(), "nebula::echo");
        CommandManager.RegisterCommand(new KillCommand(), "nebula::kill");
        CommandManager.RegisterCommand(new ReviveCommand(), "nebula::revive");
        CommandManager.RegisterCommand(new EntityCommand(), "nebula::entity");
        CommandManager.RegisterCommand(new OutfitCommand(), "nebula::outfit");
        CommandManager.RegisterCommand(new RoleCommand(), "nebula::role");
        CommandManager.RegisterCommand(new ColCommand(), "nebula::col");
        CommandManager.RegisterCommand(new DoCommand(), "nebula::do");
        CommandManager.RegisterCommand(new CastCommand(), "nebula::cast");
        CommandManager.RegisterCommand(new DoParallelCommand(), "nebula::parallel");
        CommandManager.RegisterCommand(new WaitCommand(), "nebula::wait");
        CommandManager.RegisterCommand(new RandomCommand(), "nebula::random");
        CommandManager.RegisterCommand(new ShowCommand(), "nebula::show");
        CommandManager.RegisterCommand(new EffectCommand(), "nebula::effect");
        CommandManager.RegisterCommand(new GuiHolderCommand(), "gui::holder");
        CommandManager.RegisterCommand(new GuiTextCommand(), "gui::text");
        CommandManager.RegisterCommand(new GuiButtonCommand(), "gui::button");
        CommandManager.RegisterCommand(new GuiArrayerCommand(), "gui::arrayer");
    }
}
