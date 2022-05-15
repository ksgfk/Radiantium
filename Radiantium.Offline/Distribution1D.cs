namespace Radiantium.Offline
{
    /// <summary>
    /// 一维分段常数函数
    /// https://pbr-book.org/3ed-2018/Monte_Carlo_Integration/Sampling_Random_Variables#x1-Example:Piecewise-Constant1DFunctions
    /// </summary>
    public class Distribution1D
    {
        readonly float[] _pdf;
        readonly float[] _cdf;
        readonly float _integration;

        public int Count => _pdf.Length;
        public float Integration => _integration;
        public IReadOnlyList<float> Pdf => _pdf;

        public Distribution1D(Span<float> pdf) : this(pdf.ToArray()) { }

        public Distribution1D(float[] pdf)
        {
            if (pdf.Length <= 0)
            {
                throw new ArgumentException("至少有一个pdf吧");
            }
            _pdf = pdf;
            _cdf = new float[pdf.Length + 1];
            _cdf[0] = 0;
            int n = pdf.Length;
            for (int i = 1; i < n + 1; ++i)//累加算概率密度函数的积分
            {
                _cdf[i] = _cdf[i - 1] + _pdf[i - 1] / n;
            }
            //将分段函数积分转化为概率分布函数
            //也就是归一化，把函数缩小到[0,1)
            _integration = _cdf[n];
            if (_integration == 0)
            {
                for (int i = 1; i < n + 1; i++)
                {
                    _cdf[i] = i / (float)n;
                }
            }
            else
            {
                for (int i = 1; i < n + 1; i++)
                {
                    _cdf[i] /= _integration;
                }
            }
        }

        private int FindInterval(float u) //二分查找
        {
            int first = 0;
            int len = _cdf.Length;
            while (len > 0)
            {
                int half = len >> 1;
                int middle = first + half;
                if (_cdf[middle] <= u)
                {
                    first = middle + 1;
                    len -= half + 1;
                }
                else
                {
                    len = half;
                }
            }
            return Math.Clamp(first - 1, 0, _cdf.Length - 2);
        }

        public float SampleContinuous(float u, out float pdf, out int offset)
        {
            offset = FindInterval(u);
            float du = u - _cdf[offset];
            if ((_cdf[offset + 1] - _cdf[offset]) > 0)
            {
                du /= _cdf[offset + 1] - _cdf[offset];
            }
            pdf = ContinuousPdf(offset);
            float cdf = (offset + du) / Count;
            return cdf;
        }

        public float SampleContinuous(float u)
        {
            return SampleContinuous(u, out _, out _);
        }

        public float ContinuousPdf(int offset)
        {
            return _integration > 0 ? _pdf[offset] / _integration : 0;
        }

        public int SampleDiscrete(float u, out float pdf)
        {
            int offset = FindInterval(u);
            pdf = DiscretePdf(offset);
            return offset;
        }

        public int SampleDiscrete(float u)
        {
            return SampleDiscrete(u, out _);
        }

        public float DiscretePdf(int index)
        {
            return _integration > 0 ? _pdf[index] / (_integration * Count) : 0;
        }
    }
}
