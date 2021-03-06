using System.Numerics;

namespace Radiantium.Offline
{
    /// <summary>
    /// 二维分段常数函数
    /// https://pbr-book.org/3ed-2018/Monte_Carlo_Integration/2D_Sampling_with_Multidimensional_Transformations#Piecewise-Constant2DDistributions
    /// </summary>
    public class Distribution2D
    {
        readonly Distribution1D[] _conditional;//条件分布
        readonly Distribution1D _marginal;//边缘分布

        public Distribution2D(float[] data, int nu, int nv)
        {
            if (nu * nv != data.Length)
            {
                throw new ArgumentException();
            }
            _conditional = new Distribution1D[nv];
            for (int v = 0; v < nv; v++) //计算条件概率
            {
                _conditional[v] = new Distribution1D(new Span<float>(data, v * nu, nu));
            }
            float[] marginal = new float[nv];
            for (int v = 0; v < nv; v++) //计算边缘分布
            {
                marginal[v] = _conditional[v].Integration;
            }
            _marginal = new Distribution1D(marginal);
        }

        public Vector2 SampleContinuous(Vector2 u, out float pdf, out (int, int) offset)
        {
            float d1 = _marginal.SampleContinuous(u.Y, out float pdf1, out int vj);
            float d0 = _conditional[vj].SampleContinuous(u.X, out float pdf0, out int vi);
            pdf = (float)(pdf0 * pdf1);
            offset = (vj, vi);
            return new((float)d0, (float)d1);
        }

        public Vector2 SampleContinuous(Vector2 u, out float pdf)
        {
            float d1 = _marginal.SampleContinuous(u.Y, out float pdf1, out int vi);
            float d0 = _conditional[vi].SampleContinuous(u.X, out float pdf0, out _);
            pdf = (float)(pdf0 * pdf1);
            return new((float)d0, (float)d1);
        }

        public Vector2 SampleContinuous(Vector2 u)
        {
            return SampleContinuous(u, out _);
        }

        public float ContinuousPdf(float u, float v)
        {
            int iu = Math.Clamp((int)(u * _conditional[0].Count), 0, _conditional[0].Count - 1);
            int iv = Math.Clamp((int)(v * _marginal.Count), 0, _marginal.Count - 1);
            return _conditional[iv].Pdf[iu] / _marginal.Integration;
        }
    }
}
