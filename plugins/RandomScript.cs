using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
  [Info("ResearchControl", "Vlad-00003", "1.0.4")]
  [Description("Allow you to adjust price for a research")]
  /*
   * Author info:
   *   E-mail: Vlad-00003@mail.ru
   *   Vk: vk.com/vlad_00003
   */
  class RandomScript : RustPlugin
  {



    #region Oxide hooks
    void CanLootEntity(BasePlayer player, ResearchTable table)
    {
      Puts("CanLootEntity works!");
    }

    void OnItemResearch(ResearchTable table, Item targetItem, BasePlayer player)
    {
      Puts("OnItemResearch works!");
      Puts(player.ToString());
      Puts(targetItem.ToString());
    }
    // void OnResearchCostDetermine(Item item, ResearchTable researchTable)
    // {
    //   Puts("OnResearchCostDetermine works!");
    // }

    void OnItemResearched(ResearchTable table, float chance)
    {
      Puts("OnItemResearched works!");
    }
    #endregion
  }
}