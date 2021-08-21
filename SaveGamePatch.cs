using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace ColorizerMod
{
    [HarmonyPatch(typeof(Mainframe), "SaveItem")]
    static class PatchSaveItem
    {
        // Notes: 
        // This for now saves *every* item that gets saved and not just modified ones.
        // This increases save+load times, and makes a ~30kb file (depending on the inventory and storage of the player)
        // Needs to be improved.
        // I would also like to impelement a struct that can be extended easier than padding 2x floats, but es2 is annoying.

        static void Postfix(Mainframe __instance, Transform item) {
            try {
                string filepath = __instance.GetFolderName() + Conf.saveFilename + "?tag=id_";

                List<Material> materials = Util.GetChildrenMats(item, true);
                if (materials.Count == 0) {
                    return;
                }

                Color[] itemDataColors = new Color[materials.Count];
                float[] itemDataFloats = new float[materials.Count * 2];

                for (int i = 0; i < materials.Count; i++) {
                    itemDataColors[i]       = materials [i].GetColor(Util.C_BaseColorLabel);
                    itemDataFloats[i*2]     = materials [i].GetFloat(Util.C_MetallicLabel);
                    itemDataFloats[i*2 + 1] = materials [i].GetFloat(Util.C_SmoothnessLabel);
                }

                ES2.Save(itemDataColors, filepath + "colors_" + item.GetInstanceID());
                ES2.Save(itemDataFloats, filepath + "floats_" + item.GetInstanceID());
            }
            catch {}
        }
    }

    [HarmonyPatch(typeof(Mainframe), "LoadItem")]
    [HarmonyPriority(405)] // Attempt to load after Transmogrification mod, this way our colors will load even on transmogrified armors
    static class PatchLoadItem
    {
        static void Postfix(Mainframe __instance, int id, Transform __result) {
            try {
                string filepath = __instance.GetFolderName() + Conf.saveFilename + "?tag=id_";
            if (!ES2.Exists(filepath + "colors_" + id) || !ES2.Exists(filepath + "floats_" + id)) {
                    return;
                }

                var colorData = ES2.LoadArray<Color>(filepath + "colors_" + id);
                var floatData = ES2.LoadArray<float>(filepath + "floats_" + id);

                List<Material> materials = Util.GetChildrenMats(__result, true);
            
                if (colorData.Length != materials.Count || (floatData.Length/2) != materials.Count) {
                    Util.LogWarn($"ClothShader load data missmatch sizes: {colorData.Length} vs {materials.Count} vs {floatData.Length} | " + __result.name);
                    return;
                }

                for (int i = 0; i < materials.Count; ++i) {
                    materials [i].SetColor(Util.C_BaseColorLabel, colorData [i]);
                    materials [i].SetFloat(Util.C_MetallicLabel, floatData [i*2]);
                    materials [i].SetFloat(Util.C_SmoothnessLabel, floatData [i*2 + 1]);
                }
            }
            catch {} // In any case, try not to mess up peoples saves/loads.
        }
    }
}
