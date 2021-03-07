using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Mono.Cecil.Cil;
using Poly.Physics;
using PolyPhysics;
using PolyTechFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Random = UnityEngine.Random;

namespace BetterColouredEdges
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(PolyTechFramework.PolyTechMain.PluginGuid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInProcess("Poly Bridge 2.exe")]
    public class PluginMain : PolyTechMod
    {
        public const String PluginGuid = "polytech.bettercolorededges";
        public const String PluginName = "Better Coloured Edges";
        public const String PluginVersion = "1.0.0";

        private static BepInEx.Logging.ManualLogSource staticLogger;

        public static int hotkeysToResetTo = 10;
        public static ConfigEntry<bool> ColorHydraulicPistons;
        public static ConfigEntry<bool> ColorHydraulicSleeve;
        public ConfigEntry<int> TotalHotkeys;

        public static ConfigEntry<BepInEx.Configuration.KeyboardShortcut>[] ColorHotkeys;
        public static ConfigEntry<String>[] ColorStrings;

        public ConfigEntry<BepInEx.Configuration.KeyboardShortcut> test1;

        public static Color currentColor;
        public static int edgesLeftToCreateAfterThemeChange = 0;
        public static bool keybindDown = false;
        public static Color nextDebrisColor;
        public static Color springDefault = new Color(0f,1f,1f,1f);

        public static ConfigEntry<bool> save;

        public static Color[] defaultColors = {
            Color.red, new Color(255f/255f,165f/255f,0f), Color.yellow, Color.green, Color.blue, new Color(127f/255f,0f,255f/255f)
        };
        public static Color[] ColorArr;

        void Awake()
        {
            isEnabled = true;
            staticLogger = Logger;

            var harmony = new Harmony(PluginGuid);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            
            SetupTotalHotkeys();
            SetupSettings();

            shouldSaveData = true;
            PolyTechMain.registerMod(this);
        }

        void SetupTotalHotkeys()
        {
            save = Config.Bind(Convert.ToChar(0x356) + "*General*", "Save data to layouts", true, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 0 }));
            save.SettingChanged += (o, e) => { shouldSaveData = save.Value; };
            TotalHotkeys = Config.Bind(Convert.ToChar(0x356) + "*General*", "Amount Of Keybinds (Needs Menu Reload)", hotkeysToResetTo, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 0 }));

            ColorHydraulicPistons = Config.Bind(Convert.ToChar(0x356) + "*General*", "Color Hydraulic Pistons", false, new ConfigDescription("Toggles coloring the hydraulic piston.", null, new ConfigurationManagerAttributes { Order = 0 }));
            ColorHydraulicSleeve = Config.Bind(Convert.ToChar(0x356) + "*General*", "Color Hydraulic Sleeve", true, new ConfigDescription("Toggles coloring the hydraulic sleeve.", null, new ConfigurationManagerAttributes { Order = 0 }));
            
            TotalHotkeys.SettingChanged += (o, e) =>
            {
                Logger.LogMessage("Resetting to " + TotalHotkeys.Value + " hotkeys");
                try
                {
                    hotkeysToResetTo = TotalHotkeys.Value; //Get around recursion

                    String[] colorStrs = new String[TotalHotkeys.Value];
                    BepInEx.Configuration.KeyboardShortcut[] keyboardShortcuts = new BepInEx.Configuration.KeyboardShortcut[TotalHotkeys.Value];

                    for (int i = 0; i < TotalHotkeys.Value; i++)
                    {
                        if (i < ColorHotkeys.Count())
                        {
                            colorStrs[i] = ColorStrings[i].Value;
                            keyboardShortcuts[i] = ColorHotkeys[i].Value;
                        }
                    }

                    Config.Clear();
                    SetupTotalHotkeys();
                    SetupSettings();

                    for (int i = 0; i < TotalHotkeys.Value; i++)
                    {
                        ColorStrings[i].Value = colorStrs[i];
                        ColorHotkeys[i].Value = keyboardShortcuts[i];
                    }
                }
                catch(Exception f) 
                {
                    Logger.LogError("An error has occured with resetting the hotkeys.");
                }
            };
        }
        void SetupSettings()
        {
            ColorHotkeys = new ConfigEntry<BepInEx.Configuration.KeyboardShortcut>[TotalHotkeys.Value];
            ColorStrings = new ConfigEntry<String>[TotalHotkeys.Value];
            ColorArr = new Color[TotalHotkeys.Value];

            for (int colorCountIndex = 0; colorCountIndex < TotalHotkeys.Value; colorCountIndex++)
            {
                var currentIndex = colorCountIndex;
                Color defaultColor = Color.white;
                String key = "Color " + (colorCountIndex + 1);

                //Add accent character so it orders correctly: P
                if (TotalHotkeys.Value >= 10 && colorCountIndex < 9)
                {
                    key = Convert.ToChar(0x356) + key;
                }

                //Change the default color if it's less than the length of default colors
                if (colorCountIndex < defaultColors.Length)
                {
                    defaultColor = defaultColors[colorCountIndex];
                }

                //Bind configs
                ColorHotkeys[colorCountIndex] = Config.Bind(key, "Color " + (colorCountIndex + 1) + " Keybind", new BepInEx.Configuration.KeyboardShortcut(KeyCode.None), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = currentIndex }));
                ColorStrings[colorCountIndex] = Config.Bind(key, "Color " + (colorCountIndex + 1) + " Hex Value", ColorToHexString(defaultColor), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = currentIndex }));
                int val = colorCountIndex; //Necessary to do else the function will only have TotalHotkeys.Value
                ColorStrings[colorCountIndex].SettingChanged += (o, e) => { UpdateColor(val); };
                UpdateColor(val);
            }
        }

        void UpdateColor(int index)
        {
            //staticLogger.LogMessage("Updating color "+index);
            Color color;

            //Check if the color is "random"
            if (ColorUtility.TryParseHtmlString(ColorStrings[index].Value, out color))
            {
                ColorArr[index] = color;
            }
        }

        

        void Update()
        {
            bool keyPressed = false;
            int totalkeys = TotalHotkeys.Value;
            for (int i=0; i < totalkeys; i++)
            {
                //Logger.LogMessage(i);
                if (ColorHotkeys[i % totalkeys].Value.IsPressed())
                {
                    currentColor = ColorArr[i];
                    keyPressed = true;
                    break;
                }
            }
            keybindDown = keyPressed;
            //staticLogger.LogMessage("hotkeyIndexDown: "+ hotkeyIndexDown);
        }

        string ColorToHexString(Color c)
        {
            byte[] arr = { ((byte)(c.r * 255)), ((byte)(c.g * 255)), ((byte)(c.b * 255)) };
            return "#" + BitConverter.ToString(arr).Replace("-", ""); //BitConverter adds -'s so we have to remove them
        }

        public override void setSettings(string settings)
        {  

        }
        public override string getSettings()
        {
            return "";
        }
        public override byte[] saveData(){
            return SaveManager.Serialize();
        }
        public override void loadData(byte[] bytes)
        {
            SaveManager.Deserialize(bytes);
        }

        public override void enableMod(){
            isEnabled = true;
        }




       

        [HarmonyPatch(typeof(BridgeSave), "Deserialize")]
        static class OnBridgeLoad {
            static void Postfix(BridgeSaveData saveData){
                foreach (BridgeEdge edge in BridgeEdges.m_Edges){
                    if (SaveManager.edgeData.TryGetValue(SaveManager.getEdgeKey(edge), out Color color)){
                        SetMaterialColor(edge, color);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(BridgeEdges), "CreateEdge")]
        static class Patch_BridgeEdges_CreateEdge
        {
            [HarmonyPostfix]
            static void Postfix(ref BridgeEdge __result)
            {
                if (__result)
                {
                    //staticLogger.LogMessage("edgesLeftToCreateAfterThemeChange " + edgesLeftToCreateAfterThemeChange);
                    if (GameStateManager.GetState() != GameState.SIM && (edgesLeftToCreateAfterThemeChange<=0))
                    {
                        if (keybindDown)
                        {
                            Color c = currentColor;
                            if (!__result.m_PhysicsEdge) SaveManager.edgeData[SaveManager.getEdgeKey(__result)] = c;
                            //if (c == randomColor)
                            //{
                            //    c = new UnityEngine.Color((float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble());
                            //    staticLogger.LogMessage("Generated " + __result.m_Material.m_MaterialType.ToString() + " with random color: " + c);
                            //}
                            SetMaterialColor(__result, c);
                        }
                        //else
                        //{
                        //    //staticLogger.LogMessage(__result.m_Material.m_MaterialType + " Color: " + __result.m_MeshRenderer.material.color);
                        //}
                    }
                    else
                    {
                        //Runs when re-creating colored edges when switching theme / going back to editor
                        Color colorFromList;
                        if (SaveManager.edgeData.TryGetValue(SaveManager.getEdgeKey(__result), out colorFromList)){
                            SetMaterialColor(__result, colorFromList);
                            edgesLeftToCreateAfterThemeChange--;
                        }
                    }
                }
            }
        }

        

        [HarmonyPatch(typeof(BridgeEdge), "UpdateManual")]
        static class Patch_BridgeEdge_UpdateManual
        {
            [HarmonyPostfix]
            static void Postfix(ref BridgeEdge __instance)
            {
                //if (__instance.m_OriginalColors != null)
                //{
                //    __instance.m_OriginalColors[0].a += 0.001f*GamingSpeedMultiplier.Value;
                //    SetMaterialColor(__instance, Rainbow(__instance.m_OriginalColors[0].a));
                //}
            }
        }

        [HarmonyPatch(typeof(BridgeEdgeListener), "CreateDebris")]
        static class Patch_BridgeEdgeListener_CreateDebris 
        {
            [HarmonyPrefix]
            static void Prefix(ref EdgeHandle e, ref BridgeEdge brokenEdge)
            {
                //Set the color of the debris edge
                nextDebrisColor = brokenEdge.m_MeshRenderer.material.color;
                //if (brokenEdge.m_OriginalColors != null) 
                //{
                //    nextDebrisGamerColor = brokenEdge.m_OriginalColors;
                //}
            }
        }

        [HarmonyPatch(typeof(BridgeEdgeListener), "CreateBridgeEdgeFromEdge")]
        static class Patch_BridgeEdgeListener_CreateBridgeEdgeFromEdge
        {
            [HarmonyPostfix]
            static void Postfix(ref BridgeEdge __result)
            {
                //Get the color for the next debris edge
                __result.m_MeshRenderer.material.color = nextDebrisColor;
                //if (nextDebrisGamerColor != null)
                //{
                //    __result.m_OriginalColors = nextDebrisGamerColor;
                //}
            }
        }

        [HarmonyPatch(typeof(BridgeSprings), "CreateSpring")]
        static class Patch_BridgeSprings_CreateSpring
        {
            [HarmonyPostfix]
            static void Postfix(ref BridgeSpring __result)
            {
                //Set the spring color
                if (__result)
                {
                    SetMaterialColor(__result, __result.m_ParentEdge.m_MeshRenderer.material.color);
                }
            }
        }

        [HarmonyPatch(typeof(BridgeSpring), "CreateLink")]
        static class Patch_BridgeSpring_CreateLink
        {
            [HarmonyPostfix]
            static void Postfix(ref BridgeSpring __instance, ref BridgeSpringLink __result)
            {
                if (__instance)
                {
                    if (__instance.m_ParentEdge.m_MeshRenderer.material.color != springDefault)
                    {
                        __result.m_MeshRenderer.material.color = __instance.m_ParentEdge.m_MeshRenderer.material.color;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(BridgeRopes), "Add")]
        static class Patch_BridgeRopes_Add
        {
            [HarmonyPostfix]
            static void Postfix()
            {
                if (BridgeRopes.m_BridgeRopes.Last<BridgeRope>() != null)
                {
                    SetMaterialColor(BridgeRopes.m_BridgeRopes.Last<BridgeRope>(), BridgeRopes.m_BridgeRopes.Last<BridgeRope>().m_ParentEdge.m_MeshRenderer.material.color);
                }
            }
        }

        [HarmonyPatch(typeof(BridgeEdges), "Serialize")]
        static class Patch_BridgeEdges_Serialize
        {
            [HarmonyPostfix]
            static void Postfix()
            {
                foreach (BridgeEdge edge in BridgeEdges.m_Edges)
                {
                    if (edge)
                    {
                        Color c = edge.m_MeshRenderer.material.color;
                        
                        if (edge.m_Material.m_MaterialType == BridgeMaterialType.HYDRAULICS)
                        {
                            if (ColorHydraulicSleeve.Value)
                            {
                                c = edge.m_HydraulicEdgeVisualization.GetComponentsInChildren<MeshRenderer>()[0].material.color;
                            }
                        }

                        SaveManager.edgeData[SaveManager.getEdgeKey(edge)] = c;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Panel_SandboxSettings), "OnThemeChanged")]
        static class Patch_Panel_SandboxSettings_OnThemeChanged
        {
            [HarmonyPrefix]
            static void Prefix()
            {
                edgesLeftToCreateAfterThemeChange = BridgeEdges.m_Edges.Count;
                //staticLogger.LogMessage("edgesLeftToCreateAfterThemeChange set to " + edgesLeftToCreateAfterThemeChange);
            }
        }

        [HarmonyPatch(typeof(ClipboardManager), "MaybeCopyEdgeJointSelections")]
        static class Patch_ClipboardManager_MaybeCopyEdgeJointSelections
        {
            [HarmonyPrefix]
            static void Prefix(BridgeEdge newEdge, BridgeEdge sourceEdge)
            {
                Color c = sourceEdge.m_MeshRenderer.material.color;
                if (sourceEdge.m_Material.m_MaterialType == BridgeMaterialType.HYDRAULICS)
                {
                    if (ColorHydraulicPistons.Value)
                    {
                        c = sourceEdge.m_MeshRenderer.material.color;
                    }
                    if (ColorHydraulicSleeve.Value)
                    {
                        c = sourceEdge.m_HydraulicEdgeVisualization.GetComponentsInChildren<MeshRenderer>()[0].material.color;
                    }
                }
                
                SetMaterialColor(newEdge, c); //Set copy paste material color if no color key is held down
                //if (sourceEdge.m_OriginalColors != null) { c = cycleRGB; }

            }
        }

        [HarmonyPatch(typeof(ClipboardManager), "AddEdge")] 
        static class Patch_ClipboardManager_AddEdge //Sets the Copy/Paste preview colors to the material colors
        {
            [HarmonyPostfix]
            static void Postfix(ref List<ClipboardEdge> ___m_Edges, ref GameObject ___m_ClipboardContainer)
            {
                Color color = ___m_Edges.Last<ClipboardEdge>().m_SourceBridgeEdge.m_MeshRenderer.material.color;

                if (!(___m_Edges.Last<ClipboardEdge>().m_SourceBridgeEdge.m_Material.m_MaterialType == BridgeMaterialType.SPRING && color == springDefault))
                {
                    ___m_ClipboardContainer.GetComponentsInChildren<MeshRenderer>().Last<MeshRenderer>().material.color = color;
                }
            }
        }

        [HarmonyPatch(typeof(ClipboardManager), "PasteSpring")]
        static class Patch_ClipboardManager_PasteSpring //Sets the Copy/Paste preview colors to the material colors
        {
            [HarmonyPrefix]
            static void Prefix(ref BridgeEdge newEdge, ref BridgeEdge sourceEdge)
            {
                newEdge.m_MeshRenderer.material.color = sourceEdge.m_MeshRenderer.material.color;
            }
        }

        [HarmonyPatch(typeof(BridgeRope), "UpdateManual")]
        static class Patch_BridgeRope_UpdateManual //Update rope colors for gamer mode
        {
            [HarmonyPrefix]
            static void Prefix(ref BridgeRope __instance)
            {
                //if (__instance.m_ParentEdge.m_OriginalColors != null)
                //{
                    SetMaterialColor(__instance, __instance.m_ParentEdge.m_MeshRenderer.material.color);
                //}
            }
        }

        [HarmonyPatch(typeof(BridgeSpring), "UpdateManual")]
        static class Patch_BridgeSpring_UpdateManual //Update spring colors for gamer mode
        {
            [HarmonyPrefix]
            static void Prefix(ref BridgeSpring __instance)
            {
                //if (__instance.m_ParentEdge.m_OriginalColors != null)
                //{
                    SetMaterialColor(__instance, __instance.m_ParentEdge.m_MeshRenderer.material.color);
                //}
            }
        }

        static void SetMaterialColor(BridgeEdge edge, Color c)
        {
            if (edge.m_Material.m_MaterialType == BridgeMaterialType.SPRING && edge.m_SpringCoilVisualization && edge.m_MeshRenderer.material.color != springDefault)
            {
                edge.m_SpringCoilVisualization.m_FrontLink.m_MeshRenderer.material.color = c;
                edge.m_SpringCoilVisualization.m_BackLink.m_MeshRenderer.material.color = c;
            }
            //if (c == cycleRGB)
            //{
            //    CreateRainbowStartingPoint(edge);
            //}
            if (edge.m_Material.m_MaterialType == BridgeMaterialType.HYDRAULICS)
            {
                if (ColorHydraulicPistons.Value)
                {
                    edge.m_MeshRenderer.material.color = c;
                }
                if (ColorHydraulicSleeve.Value)
                {
                    edge.CreateHydraulicVisualization();
                    edge.m_HydraulicEdgeVisualization.SetColor(c);
                }
            }
            else
            {
                edge.m_MeshRenderer.material.color = c;
            }
            //staticLogger.LogMessage("Set "+ edge.m_Material.m_MaterialType.ToString() + " edge color to: " + c);
        }

        static void SetMaterialColor(BridgeRope rope, Color c)
        {
            rope.m_PhysicsRope.lineMaterial.color = c;
            
            foreach(BridgeLink link in rope.m_Links)
            {
                link.m_Link.GetComponent<MeshRenderer>().material.color = c;
            }
            //staticLogger.LogMessage("Set " + rope.m_ParentEdge.m_Material.m_MaterialType.ToString() + " rope color to: " + c);
        }

        static void SetMaterialColor(BridgeSpring spring, Color c)
        {
            if (c != springDefault)
            {
                spring.m_ParentEdge.m_MeshRenderer.material.color = c;
                spring.m_FrontLink.m_MeshRenderer.material.color = c;
                spring.m_BackLink.m_MeshRenderer.material.color = c;
            }
        }

        //public static void CreateRainbowStartingPoint(BridgeEdge edge)
        //{
        //    var aPos = edge.m_JointA.m_BuildPos;
        //    var bPos = edge.m_JointB.m_BuildPos;
        //    var midpoint = Vector3.Lerp(aPos, bPos, 0.5f);
        //    float ret = midpoint.x*GamingDistanceXMultiplier.Value + midpoint.y*GamingDistanceYMultiplier.Value;
        //    edge.m_OriginalColors = new Color[] { new Color(0, 0, 0, ret*.1f) };
        //}
    }

    public static class SaveManager {
        public static Dictionary<string, Color> edgeData = new Dictionary<string, Color> {};

        public static byte[] Serialize(){
            List<byte> bytes = new List<byte>();
            bytes.AddRange(ByteSerializer.SerializeInt(BridgeEdges.m_Edges.Count));
            edgeData = new Dictionary<string, Color> {};
            foreach (var edge in BridgeEdges.m_Edges){
                if (edge.gameObject.activeInHierarchy){
                    Color color = edge.m_MeshRenderer.material.color;
                        
                        if (edge.m_Material.m_MaterialType == BridgeMaterialType.HYDRAULICS)
                        {
                            if (PluginMain.ColorHydraulicSleeve.Value)
                            {
                                color = edge.m_HydraulicEdgeVisualization.GetComponentsInChildren<MeshRenderer>()[0].material.color;
                            }
                        }
                    edgeData[SaveManager.getEdgeKey(edge)] = color;
                }
            }
            foreach (var key in edgeData.Keys){
                bytes.AddRange(ByteSerializer.SerializeString(key));
                bytes.AddRange(ByteSerializer.SerializeColor(edgeData[key]));
            }
            return bytes.ToArray();
        }
        public static void Deserialize(byte[] bytes){
            edgeData = new Dictionary<string, Color> {};
            int offset = 0;
            int num = ByteSerializer.DeserializeInt(bytes, ref offset);
            for (int i = 0; i < num; i++){
                var key = ByteSerializer.DeserializeString(bytes, ref offset);
                var color = ByteSerializer.DeserializeColor(bytes, ref offset);
                Debug.Log($"{i} - {color}");
                edgeData[key] = color;
            }
        }
        public static string getEdgeKey(BridgeEdge edge){
            if (edge.m_JointA.m_Guid == null || edge.m_JointB.m_Guid == null || edge.m_Material.m_MaterialType == BridgeMaterialType.INVALID){
                return "-";
            }
            return $"{edge.m_JointA.m_Guid}:{edge.m_JointB.m_Guid}:{(int)edge.m_Material.m_MaterialType}";
        }
    }
}