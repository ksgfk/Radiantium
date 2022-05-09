using Radiantium.Core;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Radiantium.Editor
{
    public class RenderManager
    {
        readonly GL _gl;

        unsafe public RenderManager(GL gl)
        {
            _gl = gl ?? throw new ArgumentNullException(nameof(gl));

            byte* version = _gl.GetString(StringName.Version);
            byte* renderer = _gl.GetString(StringName.Renderer);
            byte* vendor = _gl.GetString(StringName.Vendor);
            byte* shaderVer = _gl.GetString(StringName.ShadingLanguageVersion);

            string ver = Encoding.UTF8.GetString(version, Strlen(version));
            string rer = Encoding.UTF8.GetString(renderer, Strlen(renderer));
            string ven = Encoding.UTF8.GetString(vendor, Strlen(vendor));
            string sha = Encoding.UTF8.GetString(shaderVer, Strlen(shaderVer));

            Logger.Info($"OpenGL Version: {ver}");
            Logger.Info($"Renderer: {rer}");
            Logger.Info($"Driver Vendor: {ven}");
            Logger.Info($"GLSL Version: {sha}");

            static int Strlen(byte* str)
            {
                int len = 0;
                while (str[len] != '\0') { len++; }
                return len;
            }
        }
    }
}
