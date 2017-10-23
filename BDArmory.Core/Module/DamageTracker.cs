﻿using BDArmory.Core.Extension;
using UnityEngine;

namespace BDArmory.Core.Module
{
    public class DamageTracker : PartModule
    {
        #region KSP Fields

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Damage"),
        UI_ProgressBar(affectSymCounterparts = UI_Scene.None,controlEnabled = false,scene = UI_Scene.All,maxValue = 100000,minValue = 0,requireFullControl = false)]
        public float Damage = 0f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Armor"),
        UI_FloatRange(minValue = 1f, maxValue = 500f, stepIncrement = 5f, scene = UI_Scene.All)]
        public float Armor = 15f;

        [KSPField(isPersistant = true)]
        public bool armorSet = false;

        #endregion

        //TODO: Add setting
        private readonly float maxDamageFactor = 100f;

        private MaterialColorUpdater damageRenderer;
        private Gradient g = new Gradient();

        private Part _prefabPart;
        private bool _setupRun =  false;
        private bool _firstSetup = true;

        protected virtual void Setup()
        {
            if (_setupRun) return;
            _prefabPart = part.partInfo.partPrefab;
            _setupRun = true;
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            if (part.partInfo == null)
            {
                // Loading of the prefab from the part config
                _prefabPart = part;
                SetupPrefab();

            }
            else
            {
                // Loading of the part from a saved craft                
                if (HighLogic.LoadedSceneIsEditor)
                    Setup();
                else
                    enabled = false;
            }
        }

        protected virtual void SetupPrefab()
        {
            //var PartNode = GameDatabase.Instance.GetConfigs("PART").FirstOrDefault(c => c.name.Replace('_', '.') == part.name).config;
            //var ModuleNode = PartNode.GetNodes("MODULE").FirstOrDefault(n => n.GetValue("name") == moduleName);

            //ScaleType = new ScaleType(ModuleNode);
            //SetupFromConfig(ScaleType);
            //tweakScale = currentScale = defaultScale;

            if (part != null)
            {
                //Add Damage
                UI_ProgressBar damageFieldFlight = (UI_ProgressBar)Fields["Damage"].uiControlFlight;
                damageFieldFlight.maxValue = CalculateMaxDamage();
                damageFieldFlight.minValue = 0f;

                UI_ProgressBar damageFieldEditor = (UI_ProgressBar)Fields["Damage"].uiControlEditor;
                damageFieldEditor.maxValue = CalculateMaxDamage();
                damageFieldEditor.minValue = 0f;

                //Add Armor
                UI_FloatRange armorFieldFlight = (UI_FloatRange)Fields["Armor"].uiControlFlight;
                armorFieldFlight.maxValue = 1000f;
                armorFieldFlight.minValue = 10;

                UI_FloatRange armorFieldEditor = (UI_FloatRange)Fields["Armor"].uiControlEditor;
                armorFieldEditor.maxValue = 1000f;
                armorFieldEditor.minValue = 10f;

                part.RefreshAssociatedWindows();

                if (!armorSet) SetThickness();

            }
            else
            {
                Debug.Log("[BDArmory]:DamageTracker::OnStart part  is null");
            }
        }

        public override void OnStart(StartState state)
        {
            isEnabled = true;

            if (part != null && _firstSetup) SetupPrefab();

            //damageRenderer = new MaterialColorUpdater(this.part.transform, PhysicsGlobals.TemperaturePropertyID);          
        }        

        public override void OnUpdate()
        {
            //TODO: Add effects
            if (!HighLogic.LoadedSceneIsFlight || !FlightGlobals.ready || Damage == 0f)
            {
                return;
            }

            if (part != null && _firstSetup)
            {
                _firstSetup = false;
                SetupPrefab();
             }
           // damageRenderer?.Update(GetDamageColor());         
        }

        public float GetPartExternalScaleModifier(Part part)
        {
            double defaultScale = 1.0f;
            double currentScale = 1.0f;

            if (part.Modules.Contains("TweakScale"))
            {
                PartModule pM = part.Modules["TweakScale"];
                if (pM.Fields.GetValue("currentScale") != null)
                {
                    try
                    {
                        defaultScale = pM.Fields.GetValue<float>("defaultScale");
                        currentScale = pM.Fields.GetValue<float>("currentScale");
                    }
                    catch
                    {

                    }                    
                    return (float)(currentScale / defaultScale);
                }
            }
            return 1.0f;
        }
        
        #region Damage Functions

        private float CalculateMaxDamage()
        {              
            float maxDamage = maxDamageFactor * Mathf.Clamp(part.mass, 0.001f, 50f) * Mathf.Clamp(part.crashTolerance, 1, 25);
            Damage = maxDamage;
            return maxDamage;
        }

        public void DestroyPart()
        {
            part.temperature = part.maxTemp * 2;
            //part.explode();
        }

        public float GetMaxArmor()
        {
            UI_FloatRange armorField = (UI_FloatRange)Fields["Armor"].uiControlEditor;

            return armorField.maxValue;
        }

        public float GetMaxPartDamage()
        {
            UI_ProgressBar damageField = (UI_ProgressBar) Fields["Damage"].uiControlEditor;

            return damageField.maxValue;
        }        

        public  Color GetDamageColor()
        {
            Color color = PhysicsGlobals.BlackBodyRadiation.Evaluate(Mathf.Clamp01(part.Damage() / part.MaxDamage()));
            color.a *= PhysicsGlobals.BlackBodyRadiationAlphaMult * part.blackBodyRadiationAlphaMult; ;
            return color;
        }

         public void SetDamage(float partdamage)
        {
            Damage = partdamage;
            //if (Damage > GetMaxPartDamage())
            if(Damage <= 0)
            {
                DestroyPart();
            }
        }

        public void AddDamage(float partdamage)
        {
            if (part.name == "Weapon Manager" || part.name == "BDModulePilotAI") return;

            partdamage = Mathf.Max(partdamage, 0.01f) * -1;
            Damage += partdamage;
            //if (Damage > GetMaxPartDamage())
            if (Damage <= 0)
            {
                DestroyPart();
            }
        }

        public void ReduceArmor(float massToReduce)
        {
            Armor -= massToReduce;
            if (Armor < 0) Armor = 0;
        }

        public void SetThickness(float thickness = 0)
        {
            armorSet = true;

            if (part.FindModuleImplementing<BDArmor>())
            {                
                float armor_ = part.FindModuleImplementing<BDArmor>().ArmorThickness;
                if(armor_ != 0) Armor = armor_;                
            }
        }       

        #endregion

    }
}
