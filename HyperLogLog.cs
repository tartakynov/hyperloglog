namespace HLL
{
    using System;

    /// <summary>
    /// http://static.googleusercontent.com/external_content/untrusted_dlcp/research.google.com/en//pubs/archive/40671.pdf
    /// </summary>
    internal class HyperLogLog32
    {
        #region Static Fields

        private static readonly double Pow2_32 = Math.Pow(2, 32);

        #endregion

        #region Fields

        private readonly int[] _M; // массив регистров, макс. индекс нулевого младшего бита

        private readonly double _amMM; // корректирующий коэффициент

        private readonly int _m; // количество оценок M (2^p)

        private readonly int _p; // кол-во старших битов от хеша, используемых для вычисления m

        private readonly int _pComp; // 32 - p

        #endregion

        #region Constructors and Destructors

        public HyperLogLog32(int p) // p [4..16]
        {
            this._p = p;
            this._pComp = 32 - p;
            this._m = 1 << p;
            this._M = new int[this._m];
            switch (this._m)
            {
                case 16:
                    this._amMM = 0.637 * this._m * this._m;
                    break;
                case 32:
                    this._amMM = 0.697 * this._m * this._m;
                    break;
                case 64:
                    this._amMM = 0.709 * this._m * this._m;
                    break;
                default:
                    this._amMM = (0.7213 / (1 + 1.0799 / this._m)) * this._m * this._m;
                    break;
            }
        }

        #endregion

        #region Public Methods and Operators

        /// <summary>
        /// Добавить элемент в данное множество.
        /// </summary>
        /// <param name="value">Добавляемый элемент</param>
        public void Add(object value)
        {
            uint hash = GetHash(value.ToString());
            uint idx = hash >> (this._pComp);
            this._M[idx] = Math.Max(this._M[idx], GetRank(hash, this._pComp));
        }

        /// <summary>
        /// Возвращает оценку мощности данного множества.
        /// </summary>
        /// <returns>Оценка мощности данного множества.</returns>
        public long Count()
        {
            double c = 0;
            for (int i = 0; i < this._m; i++)
            {
                c += 1.0d / Math.Pow(2, this._M[i]);
            }

            double estimate = this._amMM / c;
            if (estimate <= (2.5 * this._m))
            {
                int v = GetZeroRegistersCount(this._m, this._M);
                if (v > 0)
                {
                    estimate = LinearCounting(this._m, v);
                }
            }
            else if (estimate > (Pow2_32 / 30d))
            {
                estimate = -Pow2_32 * Math.Log(1 - (estimate / Pow2_32));
            }

            return (int)estimate;
        }

        /// <summary>
        /// Добавить другое множество к данному множеству.
        /// </summary>
        /// <param name="that">Добавляемое множество.</param>
        public void Merge(HyperLogLog32 that)
        {
            if (that._p != this._p)
            {
                throw new Exception("Different sizes");
            }

            for (int i = 0; i < this._m; i++)
            {
                int word = 0;
                for (int j = 0; j < 8; j++)
                {
                    int mask = 0x1f << (this._p * j);
                    int thisVal = (this._M[i] & mask);
                    int thatVal = (that._M[i] & mask);
                    word |= (thisVal < thatVal) ? thatVal : thisVal;
                }

                this._M[i] = word;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Функция для вычисления хеша строки. Нужно заменить на что-то более адекватное.
        /// </summary>
        /// <param name="text">Строка для которой требуется найти хеш.</param>
        /// <returns>Хеш строки.</returns>
        private static uint GetHash(string text)
        {
            uint hash = 2166136261U;
            foreach (char t in text)
            {
                hash ^= t;
                hash *= 16777619;
            }

            return hash;
        }

        /// <summary>
        /// Возвращает количество младших нулевых бит перед первой единицей в хеше и плюс 1. 
        /// Например.: 
        /// 0 0 1 0 0 1 1 0, rank = 2
        /// 0 0 1 0 0 1 0 0, rank = 3
        /// 0 0 1 0 0 0 0 0, rank = 6
        /// </summary>
        /// <param name="hash">Хеш для которого требуется вычислить ранг</param>
        /// <param name="max">Максимально возможный ранг</param>
        /// <returns>Искомый ранг</returns>
        private static int GetRank(uint hash, int max)
        {
            int rank = 1;
            while ((hash & 1) == 0 && rank <= max)
            {
                hash >>= 1;
                rank++;
            }

            return rank;
        }

        /// <summary>
        /// Возвращает количество нулевых регистров в заданном массиве регистров.
        /// </summary>
        /// <param name="m">Количество регистров</param>
        /// <param name="registers">Макссив регистров</param>
        /// <returns>Количество нулевых регистров</returns>
        private static int GetZeroRegistersCount(int m, int[] registers)
        {
            int v = 0;
            for (int i = 0; i < m; i++)
            {
                if (registers[i] == 0)
                {
                    v++;
                }
            }

            return v;
        }

        /// <summary>
        /// Алгоритм LinearCounting.
        /// </summary>
        /// <param name="m">Количество регистров.</param>
        /// <param name="v">Количество нулевых регистров.</param>
        /// <returns>Искомая оценка мощности множества.</returns>
        private static int LinearCounting(int m, int v)
        {
            return (int)(m * Math.Log(m / (double)v));
        }

        #endregion
    }
}