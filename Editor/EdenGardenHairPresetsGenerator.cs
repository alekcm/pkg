#if UNITY_EDITOR
using System.Collections.Generic;
using CharacterEditor.Hair;
using CharacterEditor.Hair.Proc;
using UnityEditor;
using UnityEngine;

namespace CharacterEditor.Hair.EditorTool
{
    /// <summary>
    /// Generates style-specific procedural hair presets inspired by the attached Project Eden's Garden sheets.
    /// These are still editable procedural guides, not final artist meshes: they provide correct root zones,
    /// silhouettes, strand shape settings, and parameters needed for the in-game editor.
    /// </summary>
    public static class EdenGardenHairPresetsGenerator
    {
        private const string Root = "Assets/Generated/HairLibraryProc_EdenGarden";

        [MenuItem("Tools/Character/Hair/Generate Eden Garden Hair Presets")]
        public static void Generate()
        {
            EnsureFolder("Assets/Generated");
            EnsureFolder(Root);

            ScalpProfileDefinition profile = FindProfile();
            if (profile == null)
                Debug.LogWarning("[EdenHair] No ScalpProfileDefinition found. Presets will use fallback head-local roots. Create/assign a Scalp Profile for best results.");

            Save(CreateCassidy(profile), $"{Root}/Cassidy_Amber_Hair.asset");
            Save(CreateDamon(profile), $"{Root}/Damon_Maitsu_Hair.asset");
            Save(CreateEloise(profile), $"{Root}/Eloise_Taulner_Bob.asset");
            Save(CreateEva(profile), $"{Root}/Eva_Tsunaka_LongTwinCurls.asset");
            Save(CreateGrace(profile), $"{Root}/Grace_Madison_Ponytail.asset");
            Save(CreateIngrid(profile), $"{Root}/Ingrid_Grimwall_ShortMessy.asset");
            Save(CreateJean(profile), $"{Root}/Jean_Delamer_BandanaLocks.asset");
            Save(CreateKai(profile), $"{Root}/Kai_Monteago_SoftShort.asset");
            Save(CreateMark(profile), $"{Root}/Mark_Berskii_BeanieBangs.asset");
            Save(CreateDesmond(profile), $"{Root}/Desmond_Hall_DreadsPony.asset");
            Save(CreateDiana(profile), $"{Root}/Diana_Venicia_LongTies.asset");
            Save(CreateToshiko(profile), $"{Root}/Toshiko_Kayura_TwinBuns.asset");
            Save(CreateUlysses(profile), $"{Root}/Ulysses_Wilhelm_Undercut.asset");
            Save(CreateWenona(profile), $"{Root}/Wenona_TwinBraids.asset");
            Save(CreateWolfgang(profile), $"{Root}/Wolfgang_Akire_SweptUndercut.asset");

            UpdateCatalog();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Eden Garden Hair", $"Generated presets in:\n{Root}\n\nCatalog updated.\n\nTip: select your ScalpProfile asset and run Assign Selected Scalp Profile To All Hair Pieces if needed.", "OK");
        }

        // ---------- Character creators ----------

        static HairPieceDefinitionProc CreateCassidy(ScalpProfileDefinition p)
        {
            var hp = Base("cassidy_amber_hair", "Cassidy Amber - cap waves", HairSlot.Back, p, sheet:true);
            hp.defaultWave = 0.35f; hp.defaultCurl = 0.20f; hp.defaultFrizz = 0.10f;
            hp.defaultColor = Grad(0.45f,0.04f,0.06f, 0.85f,0.18f,0.14f);
            AddBangs(hp, p, 12, 0.16f, sidePart:-0.25f);
            AddSideLocks(hp, p, left:true, 8, 0.30f, 0.025f);
            AddSideLocks(hp, p, left:false, 8, 0.30f, 0.025f);
            AddBackCurtain(hp, p, 22, 0.42f, 0.060f);
            AddTopWisps(hp, p, 6, 0.08f);
            return hp;
        }

        static HairPieceDefinitionProc CreateDamon(ScalpProfileDefinition p)
        {
            var hp = Base("damon_maitsu_hair", "Damon Maitsu - short spiky", HairSlot.Back, p, sheet:true);
            hp.defaultWave = 0.18f; hp.defaultFrizz = 0.08f;
            hp.defaultColor = Grad(0.48f,0.30f,0.13f, 0.86f,0.66f,0.32f);
            AddSpikyTop(hp, p, 24, 0.10f, forwardBias:0.020f);
            AddBangs(hp, p, 10, 0.12f, sidePart:0.15f);
            AddNapeShort(hp, p, 10, 0.10f);
            return hp;
        }

        static HairPieceDefinitionProc CreateEloise(ScalpProfileDefinition p)
        {
            var hp = Base("eloise_taulner_bob", "Eloise Taulner - blunt bob", HairSlot.Back, p, sheet:true);
            hp.strandWidthScale = 3.2f; hp.strandDepthScale = 0.35f; hp.strandSides = 4;
            hp.defaultColor = Grad(0.12f,0.055f,0.055f, 0.36f,0.18f,0.18f);
            AddStraightBangs(hp, p, 18, 0.11f);
            AddSideBob(hp, p, true, 12, 0.22f);
            AddSideBob(hp, p, false, 12, 0.22f);
            AddBackCurtain(hp, p, 18, 0.25f, 0.030f);
            return hp;
        }

        static HairPieceDefinitionProc CreateEva(ScalpProfileDefinition p)
        {
            var hp = Base("eva_tsunaka_hair", "Eva Tsunaka - long curls braid", HairSlot.Back, p, sheet:false);
            hp.strandSides = 7; hp.strandWidthScale = 1.15f; hp.strandDepthScale = 1f;
            hp.defaultWave = 0.55f; hp.defaultCurl = 0.45f; hp.defaultFrizz = 0.08f;
            hp.defaultColor = Grad(0.02f,0.018f,0.015f, 0.16f,0.16f,0.15f);
            AddStraightBangs(hp, p, 16, 0.15f);
            AddBackCurtain(hp, p, 32, 0.72f, 0.120f);
            AddSideRinglets(hp, p, true, 5, 0.45f);
            AddSideRinglets(hp, p, false, 5, 0.45f);
            AddBraidSide(hp, p, right:true, 8, 0.48f);
            return hp;
        }

        static HairPieceDefinitionProc CreateGrace(ScalpProfileDefinition p)
        {
            var hp = Base("grace_madison_hair", "Grace Madison - high pony + side curls", HairSlot.Ponytail, p, sheet:true);
            hp.defaultWave = 0.35f; hp.defaultCurl = 0.25f;
            hp.defaultColor = Grad(0.68f,0.48f,0.38f, 0.95f,0.73f,0.62f);
            AddCapBangs(hp, p, 12, 0.14f);
            AddHighPonytail(hp, p, 24, 0.52f, side:1f);
            AddSideLocks(hp, p, true, 5, 0.22f, 0.045f);
            AddSideLocks(hp, p, false, 5, 0.22f, 0.045f);
            return hp;
        }

        static HairPieceDefinitionProc CreateIngrid(ScalpProfileDefinition p)
        {
            var hp = Base("ingrid_grimwall_hair", "Ingrid Grimwall - messy short", HairSlot.Back, p, sheet:true);
            hp.defaultWave = 0.25f; hp.defaultFrizz = 0.20f;
            hp.defaultColor = Grad(0.35f,0.12f,0.10f, 0.85f,0.45f,0.36f);
            AddSpikyTop(hp, p, 28, 0.13f, forwardBias:0.00f);
            AddLongSideBang(hp, p, right:false, 0.25f);
            AddNapeShort(hp, p, 12, 0.13f);
            return hp;
        }

        static HairPieceDefinitionProc CreateJean(ScalpProfileDefinition p)
        {
            var hp = Base("jean_delamer_hair", "Jean Delamer - bandana loose locks", HairSlot.Back, p, sheet:true);
            hp.defaultWave = 0.40f; hp.defaultFrizz = 0.18f;
            hp.defaultColor = Grad(0.06f,0.035f,0.025f, 0.16f,0.08f,0.05f);
            AddSweptBackTop(hp, p, 18, 0.18f);
            AddSideLocks(hp, p, true, 10, 0.36f, 0.055f);
            AddSideLocks(hp, p, false, 10, 0.36f, 0.055f);
            AddNapeShort(hp, p, 14, 0.18f);
            return hp;
        }

        static HairPieceDefinitionProc CreateKai(ScalpProfileDefinition p)
        {
            var hp = Base("kai_monteago_hair", "Kai Monteago - soft short", HairSlot.Back, p, sheet:true);
            hp.defaultWave = 0.22f; hp.defaultCurl = 0.12f;
            hp.defaultColor = Grad(0.72f,0.22f,0.42f, 0.95f,0.55f,0.72f);
            AddSoftCurtainBangs(hp, p, 18, 0.17f);
            AddSpikyTop(hp, p, 14, 0.10f, forwardBias:0.01f);
            AddNapeShort(hp, p, 12, 0.13f);
            return hp;
        }

        static HairPieceDefinitionProc CreateMark(ScalpProfileDefinition p)
        {
            var hp = Base("mark_berskii_hair", "Mark Berskii - beanie bangs", HairSlot.Bangs, p, sheet:true);
            hp.defaultWave = 0.25f; hp.defaultFrizz = 0.10f;
            hp.defaultColor = Grad(0.02f,0.018f,0.012f, 0.15f,0.14f,0.10f);
            AddMessyBeanieBangs(hp, p, 18, 0.15f);
            AddNapeShort(hp, p, 8, 0.08f);
            return hp;
        }

        static HairPieceDefinitionProc CreateDesmond(ScalpProfileDefinition p)
        {
            var hp = Base("desmond_hall_dreads_proc", "Desmond Hall - dreads ponytail", HairSlot.Back, p, sheet:false);
            hp.strandSides = 7; hp.strandWidthScale = 1f; hp.strandDepthScale = 1f;
            hp.strandSeparationRadius = 0.035f; hp.strandSeparationStrength = 0.75f;
            hp.defaultWave = 0.10f; hp.defaultCurl = 0.08f; hp.defaultFrizz = 0.35f;
            hp.defaultColor = Grad(0.10f,0.045f,0.025f, 0.45f,0.22f,0.10f);
            AddSweptBackTop(hp, p, 18, 0.18f);
            AddBackDreadsPony(hp, p, 34, 0.48f);
            AddFrontDreads(hp, p, 10, 0.34f);
            return hp;
        }

        static HairPieceDefinitionProc CreateDiana(ScalpProfileDefinition p)
        {
            var hp = Base("diana_venicia_hair", "Diana Venicia - long tied side hair", HairSlot.Back, p, sheet:true);
            hp.defaultWave = 0.35f; hp.defaultCurl = 0.20f;
            hp.defaultColor = Grad(0.42f,0.08f,0.18f, 0.85f,0.24f,0.45f);
            AddSoftCurtainBangs(hp, p, 14, 0.18f);
            AddBackCurtain(hp, p, 26, 0.62f, 0.100f);
            AddSideLocks(hp, p, true, 8, 0.42f, 0.060f);
            AddSideLocks(hp, p, false, 8, 0.42f, 0.060f);
            return hp;
        }

        static HairPieceDefinitionProc CreateToshiko(ScalpProfileDefinition p)
        {
            var hp = Base("toshiko_kayura_hair", "Toshiko Kayura - twin buns", HairSlot.Extra, p, sheet:true);
            hp.defaultWave = 0.20f; hp.defaultCurl = 0.25f;
            hp.defaultColor = Grad(0.02f,0.018f,0.015f, 0.12f,0.08f,0.06f);
            AddStraightBangs(hp, p, 18, 0.13f);
            AddTwinBuns(hp, p, 12);
            AddNapeShort(hp, p, 10, 0.12f);
            return hp;
        }

        static HairPieceDefinitionProc CreateUlysses(ScalpProfileDefinition p)
        {
            var hp = Base("ulysses_wilhelm_hair", "Ulysses Wilhelm - undercut sweep", HairSlot.Back, p, sheet:true);
            hp.defaultWave = 0.28f; hp.defaultFrizz = 0.06f;
            hp.defaultColor = Grad(0.20f,0.13f,0.08f, 0.45f,0.30f,0.18f);
            AddUndercutSweep(hp, p, 30, right:true, length:0.22f);
            return hp;
        }

        static HairPieceDefinitionProc CreateWenona(ScalpProfileDefinition p)
        {
            var hp = Base("wenona_hair", "Wenona - twin braids", HairSlot.Extra, p, sheet:false);
            hp.strandSides = 6; hp.strandWidthScale = 1.1f; hp.strandDepthScale = 1f;
            hp.defaultWave = 0.12f; hp.defaultCurl = 0.08f;
            hp.defaultColor = Grad(0.07f,0.05f,0.04f, 0.22f,0.16f,0.11f);
            AddCenterPartCurtain(hp, p, 12, 0.28f);
            AddBraidSide(hp, p, false, 12, 0.55f);
            AddBraidSide(hp, p, true, 12, 0.55f);
            return hp;
        }

        static HairPieceDefinitionProc CreateWolfgang(ScalpProfileDefinition p)
        {
            var hp = Base("wolfgang_akire_hair", "Wolfgang Akire - slick undercut", HairSlot.Back, p, sheet:true);
            hp.defaultWave = 0.12f; hp.defaultFrizz = 0.04f;
            hp.defaultColor = Grad(0.025f,0.025f,0.045f, 0.12f,0.12f,0.22f);
            AddSlickedBack(hp, p, 28, 0.20f);
            AddLongSideBang(hp, p, right:false, 0.24f);
            return hp;
        }

        // ---------- Building blocks ----------

        static HairPieceDefinitionProc Base(string id, string display, HairSlot slot, ScalpProfileDefinition p, bool sheet)
        {
            var hp = ScriptableObject.CreateInstance<HairPieceDefinitionProc>();
            hp.id = id; hp.displayName = display; hp.slot = slot;
            hp.scalpProfile = p;
            hp.headCollisionMask = p != null ? p.headMask : null;
            hp.guides = System.Array.Empty<HairGuide>();
            hp.segmentsLOD0 = 18; hp.segmentsLOD1 = 10; hp.segmentsLOD2 = 5;
            hp.maxVertices = 90000;
            hp.defaultDensity = 1f; hp.defaultLength = 1f; hp.defaultThickness = 1f;
            hp.enableStrandSeparation = true;
            hp.strandSeparationRadius = sheet ? 0.026f : 0.032f;
            hp.strandSeparationStrength = sheet ? 0.45f : 0.65f;
            hp.strandSides = sheet ? 4 : 6;
            hp.strandWidthScale = sheet ? 2.6f : 1f;
            hp.strandDepthScale = sheet ? 0.38f : 1f;
            hp.groupLengthScale = new float[] {1,1,1,1,1,1,1,1};
            return hp;
        }

        static HairColorGradient Grad(float rr,float rg,float rb,float tr,float tg,float tb)
        {
            return new HairColorGradient{ rootColor=new Color(rr,rg,rb,1), tipColor=new Color(tr,tg,tb,1), rootFade=0.25f, highlightColor=new Color(tr,tg,tb,1), highlightStrength=0.10f };
        }

        static void Add(HairPieceDefinitionProc hp, HairGuide g)
        {
            var list = new List<HairGuide>(hp.guides ?? System.Array.Empty<HairGuide>());
            list.Add(g); hp.guides = list.ToArray();
        }

        static HairGuide Guide(ScalpProfileDefinition p, ScalpZoneId zone, float u, float v, Vector3[] points, float rootThick, float tipThick, int group, int sides=4)
        {
            Vector3 root = p != null ? p.GetRoot(zone, u, v) : FallbackRoot(zone, u, v);
            return new HairGuide{ attachBone="c_head", rootLocalPos=root, rootLocalNormal=p!=null?p.GetNormal(root):Vector3.up, pointsLocal=points, thicknessRoot=rootThick, thicknessTip=tipThick, sideCount=sides, groupId=group };
        }

        static Vector3 FallbackRoot(ScalpZoneId z, float u, float v)
        {
            switch(z){
                case ScalpZoneId.BangsFront: return new Vector3(u*0.05f,0.12f,0.06f);
                case ScalpZoneId.SideLeftTop: return new Vector3(-0.075f,0.11f,v*0.02f);
                case ScalpZoneId.SideRightTop: return new Vector3(0.075f,0.11f,v*0.02f);
                case ScalpZoneId.BackCrown: return new Vector3(u*0.06f,0.13f,-0.07f);
                case ScalpZoneId.BraidLeftTop: return new Vector3(-0.06f,0.11f,-0.04f);
                case ScalpZoneId.BraidRightTop: return new Vector3(0.06f,0.11f,-0.04f);
                default: return new Vector3(u*0.04f,0.14f,v*0.04f);
            }
        }

        static float H(int i, int b){ float f=1,r=0; i++; while(i>0){ f/=b; r+=f*(i%b); i/=b; } return r; }
        static float U(int i,int b)=>H(i,b)*2f-1f;

        static Vector3[] P(Vector3 a, Vector3 b, Vector3 c) => new[]{ Vector3.zero, a, b, c };

        // Blocks
        static void AddBangs(HairPieceDefinitionProc hp, ScalpProfileDefinition p, int count, float len, float sidePart)
        { for(int i=0;i<count;i++){ float u=Mathf.Lerp(-1,1,i/(float)(count-1)); float bias=(u-sidePart)*0.025f; Add(hp, Guide(p,ScalpZoneId.BangsFront,u,U(i,3),P(new Vector3(bias,-len*.28f,.035f),new Vector3(bias*1.2f,-len*.65f,.055f),new Vector3(bias*1.4f,-len,.075f)),.006f,.0012f,0)); } }
        static void AddStraightBangs(HairPieceDefinitionProc hp, ScalpProfileDefinition p, int count, float len)
        { for(int i=0;i<count;i++){ float u=Mathf.Lerp(-1,1,i/(float)(count-1)); Add(hp, Guide(p,ScalpZoneId.BangsFront,u,0,P(new Vector3(u*.006f,-len*.30f,.035f),new Vector3(u*.010f,-len*.70f,.052f),new Vector3(u*.012f,-len,.065f)),.0055f,.001f,0)); } }
        static void AddSoftCurtainBangs(HairPieceDefinitionProc hp, ScalpProfileDefinition p, int count, float len)
        { for(int i=0;i<count;i++){ float u=Mathf.Lerp(-1,1,i/(float)(count-1)); float side=Mathf.Sign(u==0?.1f:u); Add(hp, Guide(p,ScalpZoneId.BangsFront,u,U(i,3),P(new Vector3(side*.012f,-len*.25f,.035f),new Vector3(side*.030f,-len*.60f,.045f),new Vector3(side*.055f,-len,.040f)),.006f,.001f,0)); } }
        static void AddCapBangs(HairPieceDefinitionProc hp, ScalpProfileDefinition p, int count, float len) => AddSoftCurtainBangs(hp,p,count,len);

        static void AddSideLocks(HairPieceDefinitionProc hp, ScalpProfileDefinition p, bool left, int count, float len, float outward)
        { var z=left?ScalpZoneId.SideLeftTop:ScalpZoneId.SideRightTop; float s=left?-1:1; for(int i=0;i<count;i++){ float v=U(i,2); Add(hp, Guide(p,z,U(i,3),v,P(new Vector3(s*outward*.5f,-len*.25f,.005f),new Vector3(s*outward,-len*.62f,-.010f),new Vector3(s*outward*1.4f,-len,-.020f)),.006f,.0012f,left?1:2)); } }
        static void AddSideBob(HairPieceDefinitionProc hp, ScalpProfileDefinition p, bool left, int count, float len) => AddSideLocks(hp,p,left,count,len,0.030f);
        static void AddSideRinglets(HairPieceDefinitionProc hp, ScalpProfileDefinition p, bool left, int count, float len)
        { var z=left?ScalpZoneId.SideLeftTop:ScalpZoneId.SideRightTop; float s=left?-1:1; for(int i=0;i<count;i++){ float v=U(i,2); Add(hp, Guide(p,z,U(i,3),v,P(new Vector3(s*.035f,-len*.25f,.005f),new Vector3(s*.010f,-len*.55f,-.010f),new Vector3(s*.045f,-len,-.020f)),.006f,.001f,left?1:2,6)); } }

        static void AddBackCurtain(HairPieceDefinitionProc hp, ScalpProfileDefinition p, int count, float len, float spread)
        { for(int i=0;i<count;i++){ float u=Mathf.Lerp(-1,1,i/(float)(count-1)); float v=U(i,3); Add(hp, Guide(p,ScalpZoneId.BackCrown,u,v,P(new Vector3(u*spread*.25f,-len*.25f,-.030f),new Vector3(u*spread*.55f,-len*.60f,-.060f),new Vector3(u*spread,-len,-.085f)),.0065f,.0013f,3)); } }
        static void AddBackDreadsPony(HairPieceDefinitionProc hp, ScalpProfileDefinition p, int count, float len)
        { for(int i=0;i<count;i++){ float u=Mathf.Lerp(-1,1,i/(float)(count-1)); Add(hp, Guide(p,ScalpZoneId.BackCrown,u,U(i,3),P(new Vector3(u*.010f,-len*.20f,-.050f),new Vector3(u*.025f,-len*.55f,-.080f),new Vector3(u*.040f,-len,-.100f)),.012f,.008f,3,7)); } }
        static void AddFrontDreads(HairPieceDefinitionProc hp, ScalpProfileDefinition p, int count, float len)
        { for(int i=0;i<count;i++){ float u=Mathf.Lerp(-1,1,i/(float)(count-1)); Add(hp, Guide(p,ScalpZoneId.BangsFront,u,U(i,3),P(new Vector3(u*.010f,-len*.20f,.020f),new Vector3(u*.018f,-len*.55f,.010f),new Vector3(u*.030f,-len,-.010f)),.010f,.006f,0,7)); } }
        static void AddNapeShort(HairPieceDefinitionProc hp, ScalpProfileDefinition p, int count, float len)
        { for(int i=0;i<count;i++){ float u=Mathf.Lerp(-1,1,i/(float)(count-1)); Add(hp, Guide(p,ScalpZoneId.NapeBack,u,U(i,3),P(new Vector3(u*.010f,-len*.25f,-.015f),new Vector3(u*.018f,-len*.60f,-.025f),new Vector3(u*.025f,-len,-.030f)),.005f,.001f,3)); } }
        static void AddTopWisps(HairPieceDefinitionProc hp, ScalpProfileDefinition p, int count, float len)
        { for(int i=0;i<count;i++){ float u=U(i,2), v=U(i,3); Add(hp, Guide(p,ScalpZoneId.Top,u,v,P(new Vector3(u*.020f,len*.25f,v*.020f),new Vector3(u*.035f,len*.40f,v*.030f),new Vector3(u*.050f,len*.25f,v*.040f)),.004f,.0008f,4)); } }
        static void AddSpikyTop(HairPieceDefinitionProc hp, ScalpProfileDefinition p, int count, float len, float forwardBias)
        { for(int i=0;i<count;i++){ float u=U(i,2), v=U(i,3); Add(hp, Guide(p,ScalpZoneId.Top,u,v,P(new Vector3(u*.015f,len*.30f,forwardBias+v*.010f),new Vector3(u*.035f,len*.65f,forwardBias+v*.025f),new Vector3(u*.060f,len*.25f,forwardBias+v*.040f)),.0055f,.0009f,4)); } }
        static void AddSweptBackTop(HairPieceDefinitionProc hp, ScalpProfileDefinition p, int count, float len)
        { for(int i=0;i<count;i++){ float u=U(i,2), v=U(i,3); Add(hp, Guide(p,ScalpZoneId.Top,u,v,P(new Vector3(u*.010f,-len*.12f,-.030f),new Vector3(u*.020f,-len*.35f,-.080f),new Vector3(u*.030f,-len*.65f,-.120f)),.006f,.001f,4)); } }
        static void AddSlickedBack(HairPieceDefinitionProc hp, ScalpProfileDefinition p, int count, float len) => AddSweptBackTop(hp,p,count,len);
        static void AddUndercutSweep(HairPieceDefinitionProc hp, ScalpProfileDefinition p, int count, bool right, float length)
        { float s=right?1:-1; for(int i=0;i<count;i++){ float u=U(i,2), v=U(i,3); Add(hp, Guide(p,ScalpZoneId.Top,u,v,P(new Vector3(s*.025f,-length*.20f,.015f),new Vector3(s*.065f,-length*.45f,.020f),new Vector3(s*.105f,-length,.005f)),.006f,.001f,4)); } }
        static void AddLongSideBang(HairPieceDefinitionProc hp, ScalpProfileDefinition p, bool right, float len)
        { float s=right?1:-1; var z=right?ScalpZoneId.SideRightTop:ScalpZoneId.SideLeftTop; for(int i=0;i<8;i++) Add(hp, Guide(p,z,U(i,2),U(i,3),P(new Vector3(s*.025f,-len*.25f,.025f),new Vector3(s*.050f,-len*.65f,.035f),new Vector3(s*.070f,-len,.020f)),.006f,.001f,right?2:1)); }
        static void AddMessyBeanieBangs(HairPieceDefinitionProc hp, ScalpProfileDefinition p, int count, float len) => AddBangs(hp,p,count,len,0f);
        static void AddCenterPartCurtain(HairPieceDefinitionProc hp, ScalpProfileDefinition p, int count, float len){ AddSideLocks(hp,p,true,count/2,len,.035f); AddSideLocks(hp,p,false,count/2,len,.035f); }
        static void AddBraidSide(HairPieceDefinitionProc hp, ScalpProfileDefinition p, bool right, int count, float len)
        { var z=right?ScalpZoneId.BraidRightTop:ScalpZoneId.BraidLeftTop; float s=right?1:-1; for(int i=0;i<count;i++){ float u=U(i,2), v=U(i,3); Add(hp, Guide(p,z,u,v,P(new Vector3(s*.035f,-len*.20f,-.035f),new Vector3(s*.060f,-len*.55f,-.055f),new Vector3(s*.075f,-len,-.070f)),.007f,.002f,right?2:1,6)); } }
        static void AddHighPonytail(HairPieceDefinitionProc hp, ScalpProfileDefinition p, int count, float len, float side)
        { for(int i=0;i<count;i++){ float u=U(i,2), v=U(i,3); Add(hp, Guide(p,ScalpZoneId.BackCrown,u,v,P(new Vector3(side*.020f,-len*.20f,-.060f),new Vector3(side*.055f,-len*.55f,-.090f),new Vector3(side*.110f,-len,-.130f)),.007f,.0012f,3)); } }
        static void AddTwinBuns(HairPieceDefinitionProc hp, ScalpProfileDefinition p, int count)
        { for(int i=0;i<count;i++){ float a=i/(float)count*Mathf.PI*2; Add(hp, Guide(p,ScalpZoneId.BraidLeftTop,Mathf.Cos(a),Mathf.Sin(a),P(new Vector3(-.020f+Mathf.Cos(a)*.030f,-.010f,Mathf.Sin(a)*.030f),new Vector3(-.030f+Mathf.Cos(a+1.5f)*.035f,-.020f,Mathf.Sin(a+1.5f)*.035f),new Vector3(-.020f+Mathf.Cos(a+3f)*.030f,-.030f,Mathf.Sin(a+3f)*.030f)),.008f,.002f,1)); Add(hp, Guide(p,ScalpZoneId.BraidRightTop,Mathf.Cos(a),Mathf.Sin(a),P(new Vector3(.020f+Mathf.Cos(a)*.030f,-.010f,Mathf.Sin(a)*.030f),new Vector3(.030f+Mathf.Cos(a+1.5f)*.035f,-.020f,Mathf.Sin(a+1.5f)*.035f),new Vector3(.020f+Mathf.Cos(a+3f)*.030f,-.030f,Mathf.Sin(a+3f)*.030f)),.008f,.002f,2)); } }

        // ---------- Asset/catalog ----------
        static ScalpProfileDefinition FindProfile()
        {
            var selected = Selection.activeObject as ScalpProfileDefinition;
            if (selected != null) return selected;
            string[] guids = AssetDatabase.FindAssets("t:ScalpProfileDefinition");
            if (guids.Length == 0) return null;
            return AssetDatabase.LoadAssetAtPath<ScalpProfileDefinition>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
        static void Save(HairPieceDefinitionProc hp, string path)
        {
            EnsureFolder(System.IO.Path.GetDirectoryName(path).Replace('\\','/'));
            var existing = AssetDatabase.LoadAssetAtPath<HairPieceDefinitionProc>(path);
            if (existing != null) { EditorUtility.CopySerialized(hp, existing); Object.DestroyImmediate(hp); EditorUtility.SetDirty(existing); }
            else AssetDatabase.CreateAsset(hp, path);
        }
        static void UpdateCatalog()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            string catalogPath = "Assets/Resources/HairCatalogProc.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<HairCatalogProc>(catalogPath);
            if (catalog == null) { catalog = ScriptableObject.CreateInstance<HairCatalogProc>(); AssetDatabase.CreateAsset(catalog, catalogPath); }
            catalog.pieces.Clear();
            foreach (string guid in AssetDatabase.FindAssets("t:HairPieceDefinitionProc"))
            {
                var p = AssetDatabase.LoadAssetAtPath<HairPieceDefinitionProc>(AssetDatabase.GUIDToAssetPath(guid));
                if (p != null && !catalog.pieces.Contains(p)) catalog.pieces.Add(p);
            }
            catalog.Rebuild(); EditorUtility.SetDirty(catalog);
        }
        static void EnsureFolder(string f){ if(string.IsNullOrEmpty(f)||f=="Assets"||AssetDatabase.IsValidFolder(f))return; string parent=System.IO.Path.GetDirectoryName(f).Replace('\\','/'); EnsureFolder(parent); AssetDatabase.CreateFolder(parent,System.IO.Path.GetFileName(f)); }
    }
}
#endif
