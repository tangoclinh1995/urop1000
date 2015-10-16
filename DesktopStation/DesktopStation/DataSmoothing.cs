using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DesktopStation
{
    public interface IDataSmoothing
    {
        void Reset();
        double Process(double data);
    }
    


    public class MovingAverageDataSmoothing : IDataSmoothing
    {
        public int Period { get; set; }

        Queue<double> q;
        double sum;



        public MovingAverageDataSmoothing(int period = 50)
        {
            this.Period = period;
            Reset();
        }



        public void Reset()
        {
            q = new Queue<double>();
            sum = 0;
        }



        public double Process(double data)
        {
            sum += data;
            q.Enqueue(data);

            while (q.Count > Period)
                sum -= q.Dequeue();

            return sum / q.Count;
        }
    }



    public class LowPassFilterDataSmoothing : IDataSmoothing
    {
        public double Alpha { get; set; }
        double curData;



        public LowPassFilterDataSmoothing(double alpha)
        {
            Alpha = alpha;
            Reset();
        }
        


        public void Reset()
        {
            curData = double.NaN;
        }



        public double Process(double data)
        {
            if (double.IsNaN(curData))
                curData = data;
            else
                curData += Alpha * (data - curData);

            return curData;
        }
    }



    public class HighPassFilterDataSmoothing : IDataSmoothing
    {
        public double Alpha { get; set; }



        double curData, rawData;



        public HighPassFilterDataSmoothing(double alpha)
        {
            Alpha = alpha;
            Reset();
        }



        public void Reset()
        {
            curData = double.NaN;
            rawData = double.NaN;
        }



        public double Process(double data)
        {
            if (double.IsNaN(curData))
                curData = rawData = data;
            else
            {
                curData = Alpha * (curData + data - rawData);
                rawData = data;
            }

            return curData;
        }
    }
}
