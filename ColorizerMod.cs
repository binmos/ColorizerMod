using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using System.Globalization;
using BepInEx.Configuration;

namespace ColorizerMod
{
    // Configuration variables
    public static class Conf {
        public static int ModNexusId = -1;

        public static ConfigEntry<bool> modEnabled;
        public static ConfigEntry<bool> isDebug;
        public static ConfigEntry<int> nexusID;

        public static ConfigEntry<bool> disableCustomizationButtons;
        public static ConfigEntry<bool> showExperimentalToggle;

        public static ConfigEntry<string> openWindowKeyBind;
        public static ConfigEntry<bool> openWindow;

        public static ConfigEntry<string> saveFilename;


        public static ConfigEntry<Vector2> guiWindowSize;

        public static ConfigEntry<float> guiColumnSize;
        
        public static ConfigEntry<int> guiLabelFontSize;
        public static ConfigEntry<int> guiToggleFontSize;
    }

    [BepInPlugin("binmo.ColorizerMod", "Colorizer Mod", "0.1.0")]
    public partial class BepInExPlugin : BaseUnityPlugin
    {
        private static BepInExPlugin context;
        private static Harmony _hi;

        void Awake() {
            context = this;


            Conf.modEnabled = Config.Bind("General", "Enabled", true, "Enable this mod. Note that disabling this will not restore item colors until you reload the game.");
            Conf.isDebug = Config.Bind("General", "IsDebug", true, "Enable logs.");
            Conf.nexusID = Config.Bind("General", "NexusID", Conf.ModNexusId, "Nexus mod ID for updates");


            Conf.disableCustomizationButtons = Config.Bind("Options", "Prevent Inventory Missclicks", true,
                       "Disables customization and makeup buttons while Colorizer Window is open.\n" +
                       "This is a workaround (for now) because the window currently allows mouse passthrough and accidental clicks get annoying."
                       );
            
            Conf.showExperimentalToggle = Config.Bind("Options", "Enable Experimental Button", false,
                       "Shows a toggle in the Colorizer window Gui for experimental features. These features are NOT STABLE and don't always work but might be of interest to advanced users.\n"
                       );
            
            Conf.openWindowKeyBind = Config.Bind("Options", "Colorizer Window Keybind", "q",
                       "Keybind to open the colorizer window (Only does stuff in the inventory menu).\n"
                       );


            Conf.saveFilename = Config.Bind("Options", "Save Filename", "ColorizerModData.txt",
                        "This mod uses a standalone file to save its data. You can change it here.\n" +
                        "If you encounter any issues with the mod you can backup and delete this file..\n"
                        );

            Conf.openWindow = Config.Bind("Utility", "Open Window", false, "Press this to open the window while you are in the inventory");
            
            Conf.guiWindowSize = Config.Bind("Gui", "Window Size", new Vector2(700.0f, 800.0f), "Colorizer window size");
            Conf.guiColumnSize = Config.Bind("Gui", "Column Size", 175.0f, "Size of the left (text) column in the window.");
            

            Conf.guiLabelFontSize = Config.Bind("Gui",  "Label Font Size", 12, "Font size for most of the stuff in colorizer window");
            Conf.guiToggleFontSize = Config.Bind("Gui", "Item Font Size", 14, "Font size for item names in the window. (Also help text)");

            
            Conf.openWindow.SettingChanged += (_, __) => {
                if (Conf.openWindow.Value) {
                    GUI_show = true; 
                }
                Conf.openWindow.Value = false;
            };

            _hi = Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
            Util.Log("binmo.ColorizerMod: Loaded.");
        }

        void OnDestroy() {
            Util.Log("binmo.ColorizerMod: Unpatching.");
            _hi?.UnpatchSelf();
        }

        //
        //
        // GUI
        // 
        //

        // Global GUI state
        public static bool GUI_show = true;

        public static bool GUI_helpOpen = false;
        
        static Rect GUI_windowRect = new Rect(50, 50, 700, 800);
        static Vector2 GUI_scrollPos = new Vector2(0, 0);
        static GUIStyle GUI_bgStyle;

        static bool  GUI_settings_UseHSV = true;
        static bool  GUI_settings_IncludeHidden = false;
        static bool  GUI_settings_ExperimentalFeatures = false;
        static bool  GUI_settings_UseTreeView = false;
        static bool  GUI_settings_UseListView = true;

        static bool  GUI_settings_ShowReset = false;

        static int   GUI_settings_Spacing = 10;

        static float GUI_settings_VibranceMax = 1;
        static float GUI_settings_AlphaMax = 1;
        static float GUI_settings_MetallicMax = 1;
        static float GUI_settings_SmoothnessMax = 1;

        public static Dictionary<int, bool> GUI_isOpen = new Dictionary<int, bool>();
        public static Dictionary<int, bool> GUI_autoView_isOpen = new Dictionary<int, bool>();
        public static Dictionary<int, bool> GUI_massEdit = new Dictionary<int, bool>();


        // Current customization we edit upon.
        static CharacterCustomization Customization;


        static bool GUI_hasInit = false;

        void OnGUI() {
            try {
                bool canShow = Global.code.uiInventory && Global.code.uiInventory.gameObject.activeSelf && Global.code.uiInventory.curCustomization;
                if (!canShow) {
                    return;
                }
            }
            catch {
                return;
            }

            if (Conf.openWindow.Value) {
                GUI_show = true;
            }

            if (!GUI_show) {
                return;
            }

            Customization = Global.code.uiInventory.curCustomization;

            GUI.skin.label.fontSize = Conf.guiLabelFontSize.Value;
            GUI.skin.toggle.fontSize = Conf.guiToggleFontSize.Value;

            GUI_windowRect.width = Conf.guiWindowSize.Value.x;
            GUI_windowRect.height = Conf.guiWindowSize.Value.y;

            int borderOffset = 4;
            Rect innerRect = GUI_windowRect;
            innerRect.width  -= 2*borderOffset;
            innerRect.height -= 2*borderOffset;
            innerRect.x += borderOffset;
            innerRect.y += borderOffset;

            // This sometimes breaks but I have not figured out a way to fix it.
            GUI_bgStyle =  new GUIStyle {
                normal = new GUIStyleState {
                    background = MakeTex(1, 1, new Color(0.23f, 0.18f, 0.10f, 0.9f))
                }
            };

            GUI.Box(innerRect, GUIContent.none, GUI_bgStyle);
            GUI_windowRect = GUILayout.Window(0, GUI_windowRect, GuiDrawWindow, "Colorizer Mod Window");
        }

        void GuiDrawWindow(int windowID) {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Character: " + Customization.characterName);
            if (GUILayout.Button("Toggle Help")) {
                GUI_helpOpen = !GUI_helpOpen;
            }
            GUILayout.EndHorizontal();

            if (GUI_helpOpen) {
                GuiHelpView();
            }
            else {
                GuiEditView();
            }



            if (GUILayout.Button("Close Window")) {
                GUI_show = false;
            }
            GUI.DragWindow();
        }

        void GuiHelpView() {
            // Use bigger font for help text.
            GUI.skin.label.fontSize = Conf.guiToggleFontSize.Value;
            GUILayout.BeginVertical();
            GUILayout.Label("Lorem ipsum dolor sit amet, consectetur adipiscing elit. Donec vehicula pretium accumsan. Mauris ut velit sed magna pellentesque suscipit sed scelerisque risus. Suspendisse blandit at eros eget auctor. Morbi euismod viverra dui, a iaculis augue aliquam posuere. Morbi in hendrerit ligula. Maecenas dapibus congue tellus, vitae commodo ligula posuere eu. Duis ante magna, pulvinar ut augue et, blandit ultrices libero. Mauris euismod imperdiet mollis. Praesent tincidunt, libero a tempus consequat, erat sapien porta lectus, eget pellentesque lorem mi posuere mauris. Quisque luctus ex a justo accumsan, sed hendrerit mi consectetur. Ut fermentum lacus non varius tempus. Donec sit amet mi et velit laoreet congue vel nec lacus. Proin sagittis ut est vel accumsan. Nunc finibus placerat faucibus.Vivamus volutpat nisl vitae urna laoreet, at accumsan metus laoreet.Vestibulum elementum hendrerit nunc.Quisque et velit interdum, fermentum purus sed, scelerisque arcu.Ut ut imperdiet diam, rhoncus mollis dolor.Nam faucibus lacus dui, faucibus euismod risus tincidunt quis.Sed sem ex, ullamcorper ac iaculis vel, lobortis in neque.Quisque facilisis dictum bibendum.Quisque hendrerit augue ultricies, tristique urna ut, scelerisque tortor.Nulla et nunc ac dolor imperdiet lacinia.Donec accumsan lacus sed neque mattis tempus.Fusce metus sem, ornare ac ultricies eu, efficitur non neque.Suspendisse at felis id turpis maximus scelerisque eget et nibh.Vestibulum ante ipsum primis in faucibus orci luctus et ultrices posuere cubilia curae;");
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
        }

        void GuiEditView() {
            GUILayout.BeginHorizontal();

            GUILayout.FlexibleSpace();
            string initialSettingsStr = GUI_settings_UseHSV
                ? "Hue | Saturation | Vibrance | "
                : "Red   |  Green   |   Blue   | ";

            GUILayout.Label("Columns: " + initialSettingsStr + "Alpha | Metallic | Smooth");
            GUILayout.EndHorizontal();


            GUI_scrollPos = GUILayout.BeginScrollView(GUI_scrollPos, false, true);
            GUILayout.BeginVertical();
            {
                GuiDrawContent(Customization);
            }
            GUILayout.EndVertical();
            GUILayout.EndScrollView();



            GUILayout.BeginHorizontal();
            {
                GuiHorizontalSettingBool("Include Hidden:", ref GUI_settings_IncludeHidden);
                if (Conf.showExperimentalToggle.Value) {
                    GUILayout.FlexibleSpace();
                    GuiHorizontalSettingBool("Experimental Features:", ref GUI_settings_ExperimentalFeatures);
                }
                else {
                    GUI_settings_ExperimentalFeatures = false;
                }

                if (GUI_settings_ExperimentalFeatures) {
                    GUILayout.FlexibleSpace();
                    GuiHorizontalSettingBool("Show Reset:", ref GUI_settings_ShowReset);
                    GUILayout.FlexibleSpace();
                    GuiHorizontalSettingBool("List View:", ref GUI_settings_UseListView);
                    GUILayout.FlexibleSpace();
                    GuiHorizontalSettingBool("Tree View:", ref GUI_settings_UseTreeView);
                }
                else {
                    GUI_settings_UseListView = true;
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            {

                GuiHorizontalSettingBool("Use HSV:", ref GUI_settings_UseHSV);
                GUILayout.FlexibleSpace();

                GuiHorizontalSettingFloat(
                    GUI_settings_UseHSV
                    ? "Vibrance Max:"
                    : "     RGB Max:"
                    , ref GUI_settings_VibranceMax);

                GUILayout.FlexibleSpace();
                GuiHorizontalSettingFloat("Alpha Max:", ref GUI_settings_AlphaMax);
                GUILayout.FlexibleSpace();
                GuiHorizontalSettingFloat("Metallic Max:", ref GUI_settings_MetallicMax);
                GUILayout.FlexibleSpace();
                GuiHorizontalSettingFloat("Smoothness Max:", ref GUI_settings_SmoothnessMax);
            }
            GUILayout.EndHorizontal();
        }

        void GuiRecurseTree(Transform tr, float leftPad = 0) {
            if (!tr) {
                return;
            }
            if (!GUI_settings_IncludeHidden && !tr.gameObject.activeSelf) {
                return;
            }

            var meshRenderers = tr.GetComponents<MeshRenderer>();
            var skinnedMeshRenderers = tr.GetComponents<SkinnedMeshRenderer>();

            if (meshRenderers.Length == 0 && skinnedMeshRenderers.Length == 0) {
                foreach (Transform child in tr) {
                    GuiRecurseTree(child, leftPad);
                }
                return;
            }

            GUILayout.BeginHorizontal();
            GUILayout.Space(leftPad);
            bool val = GuiDictionaryToggle(tr.GetInstanceID(), ref GUI_isOpen, tr.parent.name);
            GUILayout.EndHorizontal();
            if (val) {
                foreach (var skinMeshRender in tr.GetComponents<SkinnedMeshRenderer>()) {
                    foreach (var mat in skinMeshRender.materials) {
                        GuiDrawMaterial(mat, leftPad + 40);
                    }
                }
                foreach (var meshRenderer in tr.GetComponents<MeshRenderer>()) {
                    foreach (var mat in meshRenderer.materials) {
                        GuiDrawMaterial(mat, leftPad + 40);
                    }
                }


                foreach (Transform child in tr) {
                    GuiRecurseTree(child, leftPad);
                }
            }
        }

        void GuiAutoListView(CharacterCustomization cc) {
            var tr = cc.transform;
            if (!tr) {
                return;
            }

            if (GUI_settings_ExperimentalFeatures && GuiDictionaryToggle(tr.GetInstanceID(), ref GUI_autoView_isOpen, "Character Body", false)) {
                GUILayout.Label("\t\t[highly experimental, most of these do not save, use at your own risk]");
                GuiAutoListView_Body(cc);
            }


            var components = tr.GetComponentsInChildren<Item>(GUI_settings_IncludeHidden);

            foreach (var item in components) {
                Transform itemRoot = item.transform;
                if (!GUI_settings_IncludeHidden && !itemRoot.gameObject.activeSelf) {
                    continue;
                }

                GUILayout.BeginHorizontal();
                if (!GuiDictionaryToggle(itemRoot.GetInstanceID(), ref GUI_autoView_isOpen, item.name)) {
                    GUILayout.EndHorizontal();
                    continue;
                }
                GUILayout.FlexibleSpace();
                bool massEdit = GuiDictionaryToggle(itemRoot.GetInstanceID(), ref GUI_massEdit, "Mass Edit Category", false);
                GUILayout.Space(200);
                if (GUI_settings_ShowReset && GUILayout.Button("Reset")) {
                    var resetItem = RM.code.allItems.GetItemWithName(item.name);
                    if (resetItem) {
                        Util.Log("Reverting to: " + resetItem);
                        CopyMatsBetweenTransforms(resetItem, itemRoot);
                    }
                }
                GUILayout.EndHorizontal();

                var allmats = Util.GetChildrenMats(itemRoot, GUI_settings_IncludeHidden);

                foreach (var mat in allmats) {
                    if (GuiDrawMaterial(mat, 40) && massEdit) {
                        foreach (var matTarget in allmats) {
                            CopyMatToMat(mat, matTarget);
                        }
                    }
                }
            }
        }

        void GuiAutoListView_Body(CharacterCustomization cc) {
            var tr = cc.transform;
            if (tr.GetChild(0).name != "Genesis8Female") { // Hardcoded this, w/e matches
                GUILayout.Label("ERROR: Failed to find body");
                return;
            }
            GuiRecurseTree(tr.GetChild(0), 20);
        }


        void GuiDrawContent(CharacterCustomization cc) {
            if (GUI_settings_UseListView) {
                GuiAutoListView(cc);
            }
            else if (GUI_settings_UseTreeView) {
                GuiRecurseTree(cc.transform);
            }
            else {
                var mats = Util.GetChildrenMats(cc.transform, GUI_settings_IncludeHidden);
                foreach (var mat in mats) {
                    GuiDrawMaterial(mat);
                }
            }

        }

        void CopyMatToMat(Material source, Material target) {
            // Kinda crappy code to always query the source for this but w/e
            if (source == target) {
                return;
            }

            target.SetColor(Util.C_BaseColorLabel, source.GetColor(Util.C_BaseColorLabel));
            target.SetFloat(Util.C_MetallicLabel, source.GetFloat(Util.C_MetallicLabel));
            target.SetFloat(Util.C_SmoothnessLabel, source.GetFloat(Util.C_SmoothnessLabel));
        }

        void CopyMatsBetweenTransforms(Transform source, Transform target) {
            var srcMats = Util.GetChildrenMats(source, true);
            var trgMats = Util.GetChildrenMats(target, true);

            if (srcMats.Count != trgMats.Count) {
                Util.LogWarn($"Materials missmatch. Copy between transforms will abort. {srcMats.Count} vs {trgMats.Count} ({target})target");
                return;
            }
            for (int i = 0; i < srcMats.Count; i++) {
                CopyMatToMat(srcMats [i], trgMats [i]);
            }
        }


        bool GuiDrawMaterial(Material mat, float leftPad = 0) {
            if (!mat || mat.shader.name != "HDRP/Lit") {
                return false;
            }


            bool edited = false;
            GUILayout.BeginHorizontal();
            {
                GUILayout.Space(leftPad);
                GUILayout.Label(mat.name.Replace(" (Instance)", ""), GUILayout.Width(Conf.guiColumnSize.Value), GUILayout.MaxWidth(Conf.guiColumnSize.Value));


                edited |= GuiDrawMatColor(mat, Util.C_BaseColorLabel);
                edited |= GuiDrawMatFloat(mat, Util.C_MetallicLabel, 'M', 0, GUI_settings_MetallicMax);
                edited |= GuiDrawMatFloat(mat, Util.C_SmoothnessLabel, 'S', 0, GUI_settings_SmoothnessMax);
                
            }
            GUILayout.EndHorizontal();

            return edited;
        }
  
        bool GuiDrawMatFloat(Material mat, string matLabel, char c, float minValue = 0, float maxValue = 1) {
            float initial = mat.GetFloat(matLabel);
            float newVal = GuiSliderLabel(initial, c, minValue, maxValue);
            if (newVal != initial) {
                mat.SetFloat(matLabel, newVal);
            }
            return newVal != initial;
        }
        void GuiSpace() {
            GUILayout.Space(GUI_settings_Spacing);
        }

        bool GuiDrawMatColor(Material mat, string matLabel) {
            Color c = mat.GetColor(matLabel);
            Color initial = c;
            GUILayout.BeginHorizontal();
            
            float A = c.a; // Split alpha out to keep it when converting color spaces.
            
            if (GUI_settings_UseHSV) {
                float H, S, V;
                Color.RGBToHSV(c, out H, out S, out V);

                H = GuiSliderLabel(H, 'H');       GuiSpace();
                S = GuiSliderLabel(S, 'S');       GuiSpace();
                V = GuiSliderLabel(V, 'V', 0, GUI_settings_VibranceMax); GuiSpace();
                
                c = Color.HSVToRGB(H, S, V);
            }
            else {
                c.r = GuiSliderLabel(c.r, 'R', 0, GUI_settings_VibranceMax); GuiSpace();
                c.g = GuiSliderLabel(c.g, 'G', 0, GUI_settings_VibranceMax); GuiSpace();
                c.b = GuiSliderLabel(c.b, 'B', 0, GUI_settings_VibranceMax); GuiSpace();
            }

            A = GuiSliderLabel(A, 'A', 0, GUI_settings_AlphaMax); GuiSpace();

            GUILayout.EndHorizontal();
            c.a = A;

            if (c != initial) {
                mat.SetColor(matLabel, c);
            }
            return c != initial;
        }

        static GUIStyle GUI_sliderLabelStyle = new GUIStyle { fontSize = 12, padding = { top = 2 } };
        float GuiSliderLabel(float value, char c, float minValue = 0, float maxValue = 1, params GUILayoutOption[] options) {
            GUILayout.BeginHorizontal(options);
            GUILayout.Label(char.ToString(c), GUI_sliderLabelStyle, GUILayout.ExpandWidth(false));
            float r = GUILayout.HorizontalSlider(value, minValue, maxValue);
            GUILayout.EndHorizontal();
            return r;
        }

        void GuiHorizontalSettingFloat(string Name, ref float Value) {
            GUILayout.Label(Name, GUILayout.ExpandWidth(false));
            Value = GuiTextFloat(Value);
            if (Value == 1) {
                if (GUILayout.Button("1")) {
                    Value = 6;
                }
            }
            else if (Value == 6) {
                if (GUILayout.Button("6")) {
                    Value = 20;
                }
            }
            else {
                if (GUILayout.Button("R")) {
                    Value = 1;
                }
            }
        }

        void GuiHorizontalSettingBool(string Name, ref bool Value) {
            GUILayout.Label(Name, GUILayout.ExpandWidth(false));
            Value = GUILayout.Toggle(Value, "");
        }

        float GuiTextFloat(float Value) {
            int integerV = (int)Value;
            int.TryParse(GUILayout.TextField(integerV.ToString("D", CultureInfo.InvariantCulture), GUILayout.Width(20)), NumberStyles.Any, CultureInfo.InvariantCulture, out var x);
            return (float)x;
        }

        bool GuiDictionaryToggle(int id, ref Dictionary<int,bool> dict, string name, bool defaultValue = true) {
            if (!dict.TryGetValue(id, out var isOpen)) {
                dict.Add(id, true);
                isOpen = defaultValue;
            }
            isOpen = dict[id] = GUILayout.Toggle(isOpen, name);
            return isOpen;
        }

        static Texture2D MakeTex(int width, int height, Color col) {
            Color [] pix = new Color [width * height];
            for (int i = 0; i < pix.Length; ++i) {
                pix [i] = col;
            }
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

    }

}
