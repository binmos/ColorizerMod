using System.Collections.Generic;
using UnityEngine;

namespace ColorizerMod
{
    static class Util
    {
        // Indicies could be used for this instead of labels. This is mostly leftovers from experimenting that did not get refactored.
        public static string C_BaseColorLabel = "_BaseColor";
        public static string C_MetallicLabel = "_Metallic";
        public static string C_SmoothnessLabel = "_Smoothness";

        public static string C_ModifiableShaderName = "HDRP/Lit";

        public static void Log(string str = "") {
            if (Conf.isDebug.Value) {
                Debug.Log(str);
            }
        }
        public static void LogWarn(string str = "") {
            if (Conf.isDebug.Value) {
                Debug.LogWarning(str);
            }
        }


        public static TValue GetOrCreate<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, TValue defaultVal) {
            if (!dict.TryGetValue(key, out var val)) {
                val = defaultVal;
                dict.Add(key, val);
            }
            return val;
        }

        // Safely check for keydowns
        public static bool CheckKeyDown(string value) {
            try {
                return Input.GetKeyDown(value.ToLower());
            }
            catch {
                return false;
            }
        }

        // Parse and add all materials in this transfrom tree. (Passed in materials will be kept)
        public static void AddChildrenMaterials(Transform transform, ref List<Material> inOutMats, bool IncludeInactive = false) {
            if (!transform) {
                return;
            }
            foreach (var comp in transform.GetComponentsInChildren<SkinnedMeshRenderer>(IncludeInactive)) {
                foreach (var mat in comp.materials) {
                    if (mat.shader.name == C_ModifiableShaderName) {
                        inOutMats.Add(mat);
                    }
                }
            }
            foreach (var comp in transform.GetComponentsInChildren<MeshRenderer>(IncludeInactive)) {
                foreach (var mat in comp.materials) {
                    if (mat.shader.name == C_ModifiableShaderName) {
                        inOutMats.Add(mat);
                    }
                }
            }
        }

        // Wrapper for quickly accessing all materials of at once.
        public static List<Material> GetChildrenMats(Transform transform, bool IncludeInactive = false) {
            var list = new List<Material>();
            AddChildrenMaterials(transform, ref list, IncludeInactive);
            return list;
        }

        public static void LogMaterial(Material mat) {

            if (mat.shader.name != C_ModifiableShaderName) {
                Log("Skipping mat: " + mat.name);
                return;
            }

            Log("> " + mat.name
                + "\n\t\tAlbedo    : " + mat.GetColor("_BaseColor")
                + "\n\t\tMetallic  : " + mat.GetFloat("_Metallic")
                + "\n\t\tSmoothness: " + mat.GetFloat("_Smoothness")
            );

            //if (mat.GetColor("_EmissiveColor") != Color.black) {
            //    Log("=== NON BLACK EMISSIVE COLOR ===");
            //    Log("> " + mat.name + "\n\t\tEmissiveC : " + mat.GetColor("_EmissiveColor"));
            //}


            if (mat.GetColor("_SpecularColor") != Color.white) {
                Log("=== NON WHITE SPECULAR COLOR ===");
                Log("> " + mat.name + "\n\t\tSpecularC : " + mat.GetColor("_SpecularColor"));
            }
        }

        public static Color ToHsv(Color RGBA) {
            Color result;

            float H, S, V;
            Color.RGBToHSV(RGBA, out H, out S, out V);
            result.r = H;
            result.g = S;
            result.b = V;
            result.a = RGBA.a;
            return result;
        }

        public static Color ToRgb(Color HSVA) {
            Color result = Color.HSVToRGB(HSVA.r, HSVA.g, HSVA.b);
            result.a = HSVA.a;
            return result;
        }
    }





    static class Help
    {
        public static string helpString =
            @"
CHANGELOG: v 0.2.0

FIXES:
* Fix mod enabled config setting not actually doing anything. (Oops, sorry about that)
* Fix a texture issue with the gui that made the game go black in a few cases.
* Mouse Blocking now works only blocks clicks inside the window area.This also includes camera buttons on the left and a few more buttons.
* Window no longer opens in trading screens.

NEW:
* Reset button is no longer experimental. (It does nothing if other armor mods interfere with armors)
* Add a config setting for initial window visiblity after game launch.
* Mass edit category now only modifies the edited column (instead of all columns together)


HOW TO USE:
Info about how to use this mod is available on the mod page on nexus mods. 
";
    }
}