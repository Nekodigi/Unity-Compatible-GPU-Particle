//
// Stream - line particle system
//
using UnityEngine;


namespace Kvant
{
    [ExecuteInEditMode, AddComponentMenu("Kvant/Stream")]
    public class Stream : MonoBehaviour
    {
        #region Parameters Exposed To Editor

        public int _maxParticles = 3276800;

        public Vector3 _emitterPosition = Vector3.zero;

        public Vector3 _emitterSize = Vector3.one * 0.1f;

        public float _noiseAmplitude = 5f;

        public float _noiseFrequency = 0.5f;

        public float _noiseSpeed = 0.1f;

        public float life = 10;//0~life 

        #region Color
        //public bool constColor;
        //public Color color = Color.white;
        public Gradient colorOverLife;
        public int colorSteps = 16;
        private Texture2D colorOverLifeBuffer;
        #endregion

        #region Size
        //public bool constSize;
        //public float size = 1f;
        public AnimationCurve sizeOverLife;
        private Texture2D sizeOverLifeBuffer;
        public int sizeSteps = 16;
        #endregion

        public int _randomSeed = 0;

        public bool _debug;


        #endregion

        #region Public Properties

        public bool castShadow = true;
        public bool reserveShadow = true;

        #endregion



        #region Shader And Materials

        [SerializeField] Shader _kernelShader;
        //[SerializeField] Shader _lineShader;
        [SerializeField] Shader _debugShader;

        Material _kernelMaterial;
        [SerializeField] Material _pMaterial;
        Material _debugMaterial;

        #endregion

        #region Private Variables And Objects

        RenderTexture[] pPos = new RenderTexture[2];//particle position
        RenderTexture[] pVel = new RenderTexture[2];//particle velocity
        RenderTexture[] pCol = new RenderTexture[2];//particle velocity
        RenderTexture[] pSca = new RenderTexture[2];//particle scalar
        int READ = 0;
        int WRITE = 1;
        bool _needsReset = true;

        static MaterialPropertyBlock _mpBlock;

        static MaterialPropertyBlock mpBlock
        {
            get
            {
                if (_mpBlock == null)
                    _mpBlock = new MaterialPropertyBlock();
                return _mpBlock;
            }
        }

        #endregion

        #region Private Properties

        int numMeshes;
        int meshHeight;
        int BufferWidth = 8192;

        int BufferHeight {
            get {
                return Mathf.Clamp((_maxParticles-1) / BufferWidth + 1, 1, 8192);
            }
        }

        static float deltaTime {
            get {
                return Application.isPlaying && Time.frameCount > 1 ? Time.deltaTime : 1.0f / 10;
            }
        }

        #endregion

        #region Resource Management

        public void UpdateColorOverLifeTexture()
        {
            colorSteps = Mathf.Max(2, colorSteps);

            colorOverLifeBuffer = new Texture2D(colorSteps, 1);
            colorOverLifeBuffer.wrapMode = TextureWrapMode.Clamp;

            for (int i = 0; i <= colorSteps; i++)
            {
                colorOverLifeBuffer.SetPixel(i, 0, colorOverLife.Evaluate(1.0f / colorSteps * i));
            }
            colorOverLifeBuffer.Apply();
        }

        public void UpdateSizeOverLifeBuffer()
        {
            sizeSteps = Mathf.Max(2, sizeSteps);

            sizeOverLifeBuffer = new Texture2D(sizeSteps, 1);
            sizeOverLifeBuffer.hideFlags = HideFlags.DontSave;

            for (int i = 0; i < sizeSteps; i++)
            {
                sizeOverLifeBuffer.SetPixel(i, 0, new Color(sizeOverLife.Evaluate(1f / sizeSteps * i), 0, 0));
            }
            sizeOverLifeBuffer.Apply();
        }

        Material CreateMaterial(Shader shader)
        {
            var material = new Material(shader);
            material.hideFlags = HideFlags.DontSave;
            return material;
        }

        void ResetResources()
        {
            if (_mesh == null) _mesh = CreateMesh();

            UpdateColorOverLifeTexture();
            UpdateSizeOverLifeBuffer();
            SetNewBuffer(pPos);
            SetNewBuffer(pVel);//rot might imprimented but it is not billboard
            SetNewBuffer(pCol);
            SetNewBuffer(pSca);

            // Shader materials.
            if (!_kernelMaterial) _kernelMaterial = CreateMaterial(_kernelShader);
            if (!_debugMaterial)  _debugMaterial  = CreateMaterial(_debugShader);

            // FOR PREVIEW
            InitParticles();


            _needsReset = false;
        }

        void UpdateKernelShader()
        {
            var m = _kernelMaterial;
            SwapBuffer(pPos);
            SwapBuffer(pVel);
            SwapBuffer(pCol);
            SwapBuffer(pSca);

            m.SetTexture("_pPos", pPos[READ]);
            m.SetTexture("_pVel", pVel[READ]);
            m.SetTexture("_pCol", pCol[READ]);
            m.SetTexture("_pSca", pSca[READ]);
            m.SetTexture("_colorTable", colorOverLifeBuffer);
            m.SetTexture("_sizeTable", sizeOverLifeBuffer);
            m.SetVector("_EmitterPos", _emitterPosition);
            m.SetVector("_EmitterSize", _emitterSize);

            var np = new Vector3(_noiseFrequency, _noiseAmplitude, _noiseSpeed);
            m.SetVector("_NoiseParams", np);

            m.SetVector("_Config", new Vector4(0, life, _randomSeed, deltaTime));
        }

        #region Buffer

        RenderTexture CreateSingleBuffer()
        {
            var buffer = new RenderTexture(BufferWidth, BufferHeight, 0, RenderTextureFormat.ARGBHalf);
            buffer.hideFlags = HideFlags.DontSave;
            buffer.filterMode = FilterMode.Point;
            buffer.wrapMode = TextureWrapMode.Repeat;
            return buffer;
        }

        void SetNewBuffer(RenderTexture[] bufferSet)
        {

            if (bufferSet[READ]) DestroyImmediate(bufferSet[READ]);
            if (bufferSet[WRITE]) DestroyImmediate(bufferSet[WRITE]);

            bufferSet[READ] = CreateSingleBuffer();
            bufferSet[WRITE] = CreateSingleBuffer();
        }

        void DestroyBuffer(RenderTexture[] bufferSet)
        {
            if (bufferSet[READ]) DestroyImmediate(bufferSet[READ]);
            if (bufferSet[WRITE]) DestroyImmediate(bufferSet[WRITE]);
        }

        void SwapBuffer(RenderTexture[] bufferSet)
        {
            var temp = bufferSet[READ];
            bufferSet[READ] = bufferSet[WRITE];
            bufferSet[WRITE] = temp;
           // DestroyImmediate(temp);
        }

        #endregion

        #endregion

        #region MonoBehaviour Functions

        private void Awake()
        {
            Camera.main.depthTextureMode = DepthTextureMode.Depth;

        }

        void InitParticles()
        {
            UpdateKernelShader();
            Graphics.Blit(null, pPos[WRITE], _kernelMaterial, 0);
            Graphics.Blit(null, pVel[WRITE], _kernelMaterial, 1);
            Graphics.Blit(null, pCol[WRITE], _kernelMaterial, 2);
            Graphics.Blit(null, pSca[WRITE], _kernelMaterial, 3);
        }

        void StepParticles()
        {
            UpdateKernelShader();
            Graphics.Blit(pPos[READ], pPos[WRITE], _kernelMaterial, 4);
            Graphics.Blit(pVel[READ], pVel[WRITE], _kernelMaterial, 5);
            Graphics.Blit(pCol[READ], pCol[WRITE], _kernelMaterial, 6);
            Graphics.Blit(pSca[READ], pSca[WRITE], _kernelMaterial, 7);
        }

        void Reset()
        {
            _needsReset = true;
        }

        void OnDestroy()
        {
            DestroyBuffer(pPos);
            if (_mesh) DestroyImmediate(_mesh);
            if (pPos[WRITE]) DestroyBuffer(pPos);
            if (pVel[WRITE]) DestroyBuffer(pVel);
            if (pCol[WRITE]) DestroyBuffer(pCol);
            if (pSca[WRITE]) DestroyBuffer(pSca);
            if (_kernelMaterial)  DestroyImmediate(_kernelMaterial);
            if (_debugMaterial)   DestroyImmediate(_debugMaterial);
        }

        void Update()
        {
            if (_needsReset) ResetResources();

            if (Application.isPlaying)
            {
                StepParticles();
            }
            else
            {//for preview
                InitParticles();
                //StepParticles();
            }
            Camera.main.depthTextureMode = DepthTextureMode.Depth;

            _pMaterial.SetTexture("_pPos", pPos[WRITE]);
            _pMaterial.SetTexture("_pVel", pVel[WRITE]);
            _pMaterial.SetTexture("_pCol", pCol[WRITE]);
            _pMaterial.SetTexture("_pSca", pSca[WRITE]);
            _pMaterial.SetInt("_limit", _maxParticles);
            //_pMaterial.SetPass(0);
            _pMaterial.SetFloat("_offset", 0.0f);
            for (int i = 0; i < numMeshes; i++) {
                mpBlock.Clear();
                mpBlock.SetFloat("_offset", (float)(meshHeight * i) / BufferHeight);// (float)(meshHeight * i) / BufferHeight
                //Debug.Log((float)(meshHeight * i) / BufferHeight);
                Graphics.DrawMesh(_mesh, transform.position, transform.rotation, _pMaterial, gameObject.layer, null, 0, mpBlock, castShadow, reserveShadow);
            }
        }

        Mesh _mesh;
        Mesh CreateMesh()
        {
            var Nx = BufferWidth;
            int maxHeight = Mathf.Min(Mathf.CeilToInt(65000.0f/ BufferWidth), 8192);//8 when bufferwidth=8192
            var Ny = Mathf.Min(BufferHeight, maxHeight);
            meshHeight = Ny;
            numMeshes = Mathf.CeilToInt((float)BufferHeight / maxHeight);

            // Create vertex arrays.
            var VA = new Vector3[Nx * Ny * 4];
            var TA = new Vector2[Nx * Ny * 4];

            var Ai = 0;
            
            for (var x = 0; x < Nx; x++)
            {
                for (var y = 0; y < Ny; y++)
                {
                    VA[Ai + 0] = new Vector3(0, 0, 0);
                    VA[Ai + 1] = new Vector3(1, 0, 0);
                    VA[Ai + 2] = new Vector3(2, 0, 0);
                    VA[Ai + 3] = new Vector3(3, 0, 0);

                    var u = (float)x / Nx;
                    var v = (float)y / Ny;
                    TA[Ai] = TA[Ai + 1] = TA[Ai + 2] = TA[Ai + 3] = new Vector2(u, v);

                    Ai += 4;
                }
            }

            // Index array.
            var IA = new int[VA.Length];
            for (Ai = 0; Ai < VA.Length; Ai++) IA[Ai] = Ai;

            // Create a mesh object.
            var mesh = new Mesh();
            mesh.hideFlags = HideFlags.DontSave;
            mesh.vertices = VA;
            mesh.uv = TA;
            mesh.SetIndices(IA, MeshTopology.Quads, 0);
            mesh.Optimize();

            // Avoid being culled.
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1000);

            return mesh;
        }


        private void OnRenderObject()
        {
            //_pMaterial.SetTexture("_ParticleTex1", pPos[READ]);
            
            //_pMaterial.SetPass(0);
            //Graphics.DrawProceduralNow(MeshTopology.Quads, 4, Mathf.Min(BufferHeight*BufferWidth, _maxParticles));
        }

        void OnGUI()
        {
            if (_debug && Event.current.type.Equals(EventType.Repaint))
            {
                if (_debugMaterial && pPos[WRITE])
                {
                    var rect = new Rect(0, 0, 256, 64);
                    Graphics.DrawTexture(rect, pPos[WRITE], _debugMaterial);
                }
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(_emitterPosition, _emitterSize);
        }

        #endregion
    }
}
