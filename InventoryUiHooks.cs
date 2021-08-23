using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using ColorizerMod;
using HarmonyLib;
using UnityEngine;

namespace ClothShaders
{
    [HarmonyPatch(typeof(UIInventory), nameof(UIInventory.Update))]
    class InventoryHook
    {
        static void Postfix(UIInventory __instance) {
            if (!Conf.modEnabled.Value) {
                return;
            }

            // Add a modifier key?
            // Also, maybe we could do this OnGUI and check if the user in the inventory screen?
            if (Util.CheckKeyDown(Conf.openWindowKeyBind.Value)) {
                BepInExPlugin.GUI_show = !BepInExPlugin.GUI_show;
            }
        }

    }



    [HarmonyPatch]
    class DisableClickthoughMultiPatch
    {
        static IEnumerable<MethodBase> TargetMethods() {
            // Its kinda shit to hook all these functions, but I can't think of another way right now.
            var result = 
                typeof(UIInventory).GetMethods()
                    .Where(method => method.ReturnType == typeof(void) && method.Name.StartsWith("Button")) // all functions that return void and their name starts with "Button"
                    .Cast<MethodBase>();
            return result;
        }


        static bool Prefix(MethodBase __originalMethod) {
            if (!BepInExPlugin.ShouldCaptureMouse()) {
                return true;
            }
            return false;
        }
    }


    // Currently unused, can be integrated but we would have to somehow solve the vibrance problem. (also i cba to fix the pointer being in the wrong position every time)
    // Left here to be used in the future by me (or other modders, feel free!). Didn't feel like wasting all this time spent researching.

    // NOTE 1: Stuff here might be broken, but the idea works.
    // NOTE 2: This should be extended to store a callback func.
    class ColorPicker
    {
        static string ColorizerModBtnValue = "binmos.ColorizerMod";

        
        public static void Open(Color withColor) {
            Global.code.uiColorPick.Open(withColor, ColorizerModBtnValue);
        }
        public static void Close() {
            Global.code.uiColorPick.Close();
        }
        public static bool IsOpen() {
            return Global.code.uiColorPick.gameObject.activeSelf;
        }

          
        // [HarmonyPatch(typeof(UIColorPick), "UpdateColor")]
        class ColorPickerUpdateHook
        {
            static void Postfix(UIColorPick __instance) {
                if (GetCurplace(__instance) == ColorizerModBtnValue) {
                    Color selectedColor = __instance.Paint.color;
                    // Implement callback here.
                }
            }

        }

        // Utilities to access private "curplace"
        static string GetCurplace(UIColorPick inst) {
            return typeof(UIColorPick).GetField("curplace", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(inst) as string;
        }
        static void SetCurplace(UIColorPick inst, string value) {
            typeof(UIColorPick).GetField("curplace", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(inst, value);
        }
    }
}
