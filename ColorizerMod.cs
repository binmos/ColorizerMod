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

//
// v 0.2.0: Savegame compatible
// 
// FIXES:
// * Fix mod enabled config setting not actually doing anything. (Oops, sorry about that)
// * Fix a texture issue with the gui that made the game go black in a few cases. 
// * Clicks to buttons now only block inside the window area.
// * More buttons are properly disabled behind the window. (Inventory & Portraits are still clickable)
// * Window no longer opens in trading screens.
// 
// NEW:
// * Reset button is no longer experimental. (It does nothing if other armor mods interfere with armors)
// * Add a config setting for initial window visibility after game launch.
// * Mass edit category now only modifies the edited column (instead of all columns together)
// 



namespace ColorizerMod
{
    // Configuration variables
    public static class Conf {
        public static int ModNexusId = 90;

        public static ConfigEntry<bool> modEnabled;
        public static ConfigEntry<bool> isDebug;
        public static ConfigEntry<int> nexusID;

        public static ConfigEntry<bool> disableClickthrough;
        public static ConfigEntry<bool> showExperimentalToggle;
        public static ConfigEntry<bool> refreshInventory;

        public static ConfigEntry<string> openWindowKeyBind;
        public static ConfigEntry<bool> openWindow;
        public static ConfigEntry<bool> openWindowOnGameLaunch;

        public static ConfigEntry<string> saveFilename;


        public static ConfigEntry<Vector2> guiWindowSize;

        public static ConfigEntry<float> guiColumnSize;
        
        public static ConfigEntry<int> guiLabelFontSize;
        public static ConfigEntry<int> guiToggleFontSize;
    }

    [BepInPlugin("binmos.ColorizerMod", "Colorizer Mod", "0.2.0")]
    public partial class BepInExPlugin : BaseUnityPlugin
    {
        private static BepInExPlugin context;
        private static Harmony _hi;

        void Awake() {
            context = this;


            Conf.modEnabled = Config.Bind("General", "Enabled", true, "Enable this mod. Note that disabling this will not restore item colors until you reload the game.");
            Conf.isDebug = Config.Bind("General", "IsDebug", true, "Enable logs.");
            Conf.nexusID = Config.Bind("General", "NexusID", Conf.ModNexusId, "Nexus mod ID for updates");


            Conf.disableClickthrough = Config.Bind("Options", "Prevent Inventory Missclicks", true,
                       "Disables multiple (but not all) buttons from getting clicked while the cursor is over the Colorier Window.\n" +
                       "Some stuff is still clickable (Portraits, Lingerie and Inventory) but this is much better compared to the previous version."
                       );
            
            Conf.showExperimentalToggle = Config.Bind("Options", "Enable Experimental Button", false,
                       "Shows a toggle in the Colorizer window Gui for experimental features. These features are NOT STABLE and don't always work but might be of interest to advanced users.\n"
                       );
            
            Conf.openWindowKeyBind = Config.Bind("Options", "Colorizer Window Keybind", "q",
                       "Keybind to open the colorizer window (Only does stuff in the inventory menu).\n"
                       );

            Conf.refreshInventory = Config.Bind("Options", "Refresh Inventory Workaround", true, 
                        "Refreshes the inventory when you first open the window, in an attempt to fix a bug that occurs in the base game where colors don't update instantly.\n"+
                        "If this occurs to you, just click on the portrait of the character again and the colors should start updating (usually).");

            Conf.saveFilename = Config.Bind("Options", "Save Filename", "ColorizerModData.txt",
                        "This mod uses a standalone file to save its data. You can change it here.\n" +
                        "If you encounter any issues with the mod you can backup and delete this file..\n"
                        );

            Conf.openWindowOnGameLaunch = Config.Bind("Options", "Window Starts Open", false, "When enabled the window will automatically open when you first enter the inventory when the game starts. After that it will remember your last inventory exit.");
            Conf.openWindow = Config.Bind("Utility", "Open Window", false, "Press this to open the Colorizer window (only valid in the configurator). You NEED to be in the inventory for this to do anything, but it can be used instead of a keybind.");



            Conf.guiWindowSize = Config.Bind("Gui", "Window Size", new Vector2(700.0f, 800.0f), "Colorizer window size");
            Conf.guiColumnSize = Config.Bind("Gui", "Column Size", 175.0f, "Size of the left (text) column in the window.");
            

            Conf.guiLabelFontSize = Config.Bind("Gui",  "Label Font Size", 12, "Font size for most of the stuff in colorizer window");
            Conf.guiToggleFontSize = Config.Bind("Gui", "Item Font Size", 14, "Font size for item names in the window.");

            
            Conf.openWindow.SettingChanged += (_, __) => {
                if (Conf.openWindow.Value) {
                    GUI_show = true; 
                }
                Conf.openWindow.Value = false;
            };

            _hi = Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
            Util.Log("binmos.ColorizerMod: Loaded.");
        }

        void OnDestroy() {
            Util.Log("binmos.ColorizerMod: Unpatching.");
            _hi?.UnpatchSelf();
        }

        //
        //
        // GUI
        // 
        //

        // Global GUI state, unfortunately we can't do much better and we need all this stuff due to the nature of immediate mode guis
        public static bool GUI_show = false;

        public static bool GUI_helpOpen = false;
        
        static Rect GUI_windowRect = new Rect(50, 50, 700, 800);
        static Vector2 GUI_scrollPos = new Vector2(0, 0);
        static GUIStyle GUI_bgStyle;

        static bool  GUI_settings_UseHSV = true;
        static bool  GUI_settings_IncludeHidden = false;
        static bool  GUI_settings_ExperimentalFeatures = false;
        static bool  GUI_settings_UseTreeView = false;
        static bool  GUI_settings_UseListView = true;

        static bool  GUI_settings_ShowReset = true;

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


        static bool GUI_lastFrameShow = false;
        static bool GUI_isMouseOverWindow = false;

        // Helper function to keep track of if we are on the right screen to show the window.
        static bool CanShowGuiWindow() {
            bool result = false;
            try {
                result = Global.code.uiInventory
                && Global.code.uiInventory.gameObject.activeSelf
                && Global.code.uiInventory.curCustomization
                && !Player.code.customization.curInteractionLoc
                && !Global.code.uiTrading.gameObject.activeSelf;
            }
            catch {}
            return result;
        }

        static bool GUI_hasInitOnce = false;
        static void GuiFistInit() {
            if (!GUI_hasInitOnce) {
                GUI_show = Conf.openWindowOnGameLaunch.Value;
                GUI_hasInitOnce = true;
            }
        }

        void OnGUI() {
            if (!Conf.modEnabled.Value) {
                return;
            }
            
            if (!CanShowGuiWindow()) {
                GUI_lastFrameShow = false;
                return;
            }
            
            
            if (Conf.openWindow.Value) {
                GUI_show = true;
            }

            GuiFistInit();

            if (!GUI_show) {
                GUI_lastFrameShow = false;
                return;
            }
            
            Customization = Global.code.uiInventory.curCustomization;
            if (!GUI_lastFrameShow && Conf.refreshInventory.Value) {
                Global.code.uiInventory.Open(Customization);
            }
            GUI_lastFrameShow = true;


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


            GUI_bgStyle = GuiCheckBackgroundTexture();

            GUI.Box(innerRect, GUIContent.none, GUI_bgStyle);
            GUI_windowRect = GUILayout.Window(0, GUI_windowRect, GuiDrawWindow, "Colorizer Mod Window");
        }



        public static bool ShouldCaptureMouse() {

            return Conf.modEnabled.Value            // Mod needs to be enabled
                && Conf.disableClickthrough.Value   // Config setup to use the feature
                && GUI_isMouseOverWindow           // If we are actually over the window
                && GUI_show;                        // and the window is vsiible
        }

        // Should be called from inside the window stack of OnGui.
        void UpdateMouseOver() {
            var mousePos = Event.current.mousePosition;
            GUI_isMouseOverWindow = mousePos.x < GUI_windowRect.width && mousePos.y < GUI_windowRect.height && mousePos.x > 0 && mousePos.y > 0;
        }

        void GuiDrawWindow(int windowID) {
            UpdateMouseOver();

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
            GUILayout.Label(Help.helpString);
            GUILayout.FlexibleSpace();
            if (Conf.showExperimentalToggle.Value && GUILayout.Button("Remake window background")) {
                GuiCheckBackgroundTexture(true);
            }
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
                GUILayout.FlexibleSpace();
                GuiHorizontalSettingBool("Show Reset:", ref GUI_settings_ShowReset);
                if (Conf.showExperimentalToggle.Value) {
                    GUILayout.FlexibleSpace();
                    GuiHorizontalSettingBool("Experimental Features:", ref GUI_settings_ExperimentalFeatures);
                }
                else {
                    GUI_settings_ExperimentalFeatures = false;
                }

                if (GUI_settings_ExperimentalFeatures) {
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
                    if (GuiDrawMaterial(mat, out int column, 40) && massEdit && column != -1) {
                        foreach (var matTarget in allmats) {
                            CopyMatToMatColumn(mat, matTarget, column);
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

        void CopyMatToMatColumn(Material source, Material target, int column) {
            
            if (source == target) {
                return;
            }

            // This column thing could be better, and it will get messy if more columns are added.
            if (column == -1) {
                return;
            }

            // handle metallic and smoothness early and exit.
            if (column > 3) {
                // metallic or smoothness

                switch (column) {
                    case 4: // Metallic
                        target.SetFloat(Util.C_MetallicLabel, source.GetFloat(Util.C_MetallicLabel));
                    break;
                    case 5: // Smoothness
                        target.SetFloat(Util.C_SmoothnessLabel, source.GetFloat(Util.C_SmoothnessLabel));
                    break;
                }
                return;
            }

            // 
            bool isHsv = GUI_settings_UseHSV;

            Color srcRgba = source.GetColor(Util.C_BaseColorLabel);
            Color trgRgba = target.GetColor(Util.C_BaseColorLabel);

            if (!isHsv || column == 3) { // alpha
                trgRgba [column] = srcRgba [column];
                target.SetColor(Util.C_BaseColorLabel, trgRgba);
                return;
            }

            // Decompose to hsv, copy one value and compose again
            Color srcHsva = Util.ToHsv(srcRgba);
            Color trgHsva = Util.ToHsv(trgRgba);

            trgHsva [column] = srcHsva [column];
            target.SetColor(Util.C_BaseColorLabel, Util.ToRgb(trgHsva));
        }

        void CopyMatsBetweenTransforms(Transform source, Transform target, bool validateNames = true) {
            var srcMats = Util.GetChildrenMats(source, true);
            var trgMats = Util.GetChildrenMats(target, true);

            if (srcMats.Count != trgMats.Count) {
                Util.LogWarn($"Materials missmatch. Copy between transforms will abort. {srcMats.Count} vs {trgMats.Count} ({target})target");
                return;
            }

            // TODO: this is already much better, but in we should actually check the meshes & other stuff here to be correct
            if (validateNames) {
                for (int i = 0; i < srcMats.Count; i++) {
                    if (srcMats[i].name != trgMats[i].name) {
                        Util.LogWarn($"Materials name missmatch. Copy between transforms will abort: MatIndex: {i} | {srcMats[i].name} vs {trgMats[i].name} ({target} was the target)");
                        return;
                    }
                }
            }

            for (int i = 0; i < srcMats.Count; i++) {
                CopyMatToMat(srcMats [i], trgMats [i]);
            }
        }


        bool GuiDrawMaterial(Material mat, float leftPad = 0) {
            return GuiDrawMaterial(mat, out _, leftPad);
        }

        bool GuiDrawMaterial(Material mat, out int editedColumn, float leftPad = 0) {
            editedColumn = -1;

            if (!mat || mat.shader.name != "HDRP/Lit") {
                return false;
            }


            GUILayout.BeginHorizontal();
            {
                GUILayout.Space(leftPad);
                GUILayout.Label(mat.name.Replace(" (Instance)", ""), GUILayout.Width(Conf.guiColumnSize.Value), GUILayout.MaxWidth(Conf.guiColumnSize.Value));

                
                
                editedColumn = GuiDrawMatColor(mat, Util.C_BaseColorLabel);

                if (GuiDrawMatFloat(mat, Util.C_MetallicLabel, 'M', 0, GUI_settings_MetallicMax)) {
                    editedColumn = 4;
                }
                if (GuiDrawMatFloat(mat, Util.C_SmoothnessLabel, 'S', 0, GUI_settings_SmoothnessMax)) {
                    editedColumn = 5;
                }
                
            }
            GUILayout.EndHorizontal();
            return editedColumn != -1;
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

        // Returns column index that was edited (relative to itself in 0-3 range, or -1 if not edited) 
        int GuiDrawMatColor(Material mat, string matLabel) {
            Color c = mat.GetColor(matLabel);
            Color initial = c;
            GUILayout.BeginHorizontal();
            
            float A = c.a; // Split alpha out to keep it when converting color spaces.

            int column = -1;

            if (GUI_settings_UseHSV) {
                float H, S, V;
                Color.RGBToHSV(c, out H, out S, out V);


                H = GuiSliderLabelEx(0, ref column, H, 'H');       GuiSpace();
                S = GuiSliderLabelEx(1, ref column, S, 'S');       GuiSpace();
                V = GuiSliderLabelEx(2, ref column, V, 'V', 0, GUI_settings_VibranceMax); GuiSpace();
                
                c = Color.HSVToRGB(H, S, V);
            }
            else {
                c.r = GuiSliderLabelEx(0, ref column, c.r, 'R', 0, GUI_settings_VibranceMax); GuiSpace();
                c.g = GuiSliderLabelEx(1, ref column, c.g, 'G', 0, GUI_settings_VibranceMax); GuiSpace();
                c.b = GuiSliderLabelEx(2, ref column, c.b, 'B', 0, GUI_settings_VibranceMax); GuiSpace();
            }

            A = GuiSliderLabelEx(3, ref column, A, 'A', 0, GUI_settings_AlphaMax); GuiSpace();

            GUILayout.EndHorizontal();
            c.a = A;

            if (c != initial) { // only set color if it is different.
                mat.SetColor(matLabel, c);
            }
            return column;
        }

        static GUIStyle GUI_sliderLabelStyle = new GUIStyle { fontSize = 12, padding = { top = 2 } };
        float GuiSliderLabel(float value, char c, float minValue = 0, float maxValue = 1, params GUILayoutOption[] options) {
            GUILayout.BeginHorizontal(options);
            GUILayout.Label(char.ToString(c), GUI_sliderLabelStyle, GUILayout.ExpandWidth(false));
            float r = GUILayout.HorizontalSlider(value, minValue, maxValue);
            GUILayout.EndHorizontal();
            return r;
        }

        float GuiSliderLabelEx(int thisIndex, ref int column, float value, char c, float minValue = 0, float maxValue = 1, params GUILayoutOption [] options) {
            float r = GuiSliderLabel(value, c, minValue, maxValue, options);
            if (r != value) { // NOTE: float equals, might be prone to errors.
                column = thisIndex;
            }
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

        // This will remake the texture if it somehow does not exist.
        GUIStyle GuiCheckBackgroundTexture(bool forceRemake = false) {
            bool needRemake = true;
            
            try {
                needRemake =
                    GUI_bgStyle.normal.background.width == 0;
            } catch {}

            if (needRemake || forceRemake) {
                GUI_bgStyle =  new GUIStyle {
                    normal = new GUIStyleState {
                        background = MakeTex(1, 1, new Color(0.23f, 0.18f, 0.10f, 0.9f))
                    }
                };
            }            
            return GUI_bgStyle;
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
