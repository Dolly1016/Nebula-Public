using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;
using Virial.Configuration;

namespace Nebula.Configuration;

internal class TaskConfiguration : ITaskConfiguration
{
    private ValueConfiguration<int> TopConfiguration;
    private IntegerConfiguration NumOfTasksConfiguration;
    private IntegerConfiguration NumOfShortTasksConfiguration;
    private IntegerConfiguration NumOfLongTasksConfiguration;
    private IntegerConfiguration? NumOfCommonTasksConfiguration;
    private Func<bool>? predicate;
    private bool forceTasks;
    private int simpleIndex, advancedIndex;
    public TaskConfiguration(string id, bool forceTasks, bool containCommonTasks, Func<bool>? predicate, string? translationKey)
    {
        this.forceTasks = forceTasks;
        simpleIndex = forceTasks ? 0 : 1;
        advancedIndex = forceTasks ? 1 : 2;

        if (forceTasks)
            TopConfiguration = NebulaAPI.Configurations.Configuration(id, ["options.common.task.force.simple", "options.common.task.force.advanced"], 0, title: GUI.API.LocalizedTextComponent(translationKey ?? "options.common.task.force"));
        else
            TopConfiguration = NebulaAPI.Configurations.Configuration(id, ["options.common.task.none", "options.common.task.simple", "options.common.task.advanced"], 0, title: GUI.API.LocalizedTextComponent(translationKey ?? "options.common.task"));

        NumOfTasksConfiguration = NebulaAPI.Configurations.Configuration(id + ".num", (1, 15), 5, () => TopConfiguration.GetValue() == simpleIndex, title: GUI.API.LocalizedTextComponent("options.common.task.num"));
        NumOfShortTasksConfiguration = NebulaAPI.Configurations.Configuration(id + ".numShort", (1, 10), 3, () => TopConfiguration.GetValue() == advancedIndex, title: GUI.API.LocalizedTextComponent("options.common.task.numShort"));
        NumOfLongTasksConfiguration = NebulaAPI.Configurations.Configuration(id + ".numLong", (1, 10), 1, () => TopConfiguration.GetValue() == advancedIndex, title: GUI.API.LocalizedTextComponent("options.common.task.numLong"));
        if (containCommonTasks)
            NumOfCommonTasksConfiguration = NebulaAPI.Configurations.Configuration(id + ".numCommon", (1, 5), 1, () => TopConfiguration.GetValue() == advancedIndex, title: GUI.API.LocalizedTextComponent("options.common.task.numCommon"));
        else
            NumOfCommonTasksConfiguration = null;

        this.predicate = predicate;
    }

    public IEnumerable<IConfiguration> Configurations => NumOfCommonTasksConfiguration != null ?
            [TopConfiguration, NumOfTasksConfiguration, NumOfShortTasksConfiguration, NumOfLongTasksConfiguration, NumOfCommonTasksConfiguration] :
            [TopConfiguration, NumOfTasksConfiguration, NumOfShortTasksConfiguration, NumOfLongTasksConfiguration];

    IConfiguration ITaskConfiguration.AsGroup(Virial.Color color)
    {
        return new GroupConfiguration("options.common.group.task", Configurations, color.ToUnityColor(), predicate);
    }

    private bool ContainsCommonTasks => NumOfCommonTasksConfiguration != null;
    void ITaskConfiguration.GetTasks(out int shortTasks, out int longTasks, out int commonTasks)
    {
        if(TopConfiguration.GetValue() == simpleIndex)
        {
            int numOfTasks = NumOfTasksConfiguration;
            if (numOfTasks <= 2)
            {
                shortTasks = numOfTasks;
                longTasks = 0;
                commonTasks = 0;
            }
            else
            {
                commonTasks = 0;
                shortTasks = 0;
                longTasks = 0;
                for (int i = 0; i < numOfTasks; i++)
                {
                    if (ContainsCommonTasks && Helpers.Prob(0.2f) && commonTasks < 2)
                        commonTasks++;
                    else if (Helpers.Prob(0.3f))
                        longTasks++;
                    else
                        shortTasks++;
                }
            }
        }
        else if (TopConfiguration.GetValue() == advancedIndex)
        {
            shortTasks = NumOfShortTasksConfiguration;
            longTasks = NumOfLongTasksConfiguration;
            commonTasks = NumOfCommonTasksConfiguration?.GetValue() ?? 0;
        }
        else
        {
            shortTasks = 0;
            longTasks = 0;
            commonTasks = 0;
        }

        if (forceTasks && shortTasks + longTasks + commonTasks == 0) shortTasks = 1;
    }

    public bool RequiresTasks
    {
        get
        {
            if (forceTasks) return true;
            if (/* !forceTasks && */ TopConfiguration.GetValue() == 0) return false;
            if(TopConfiguration.GetValue() == advancedIndex)
            {
                if (NumOfShortTasksConfiguration == 0 && NumOfLongTasksConfiguration == 0 && (NumOfCommonTasksConfiguration ?? 0) == 0) return false;
            }

            return true;
        }
    }
}
