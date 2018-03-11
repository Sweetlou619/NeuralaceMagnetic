using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NeuralaceMagnetic.Controls
{
    public class KalmanFilter
    {
        //private const double Q = 0.000001;
        //private const double R = 0.01;
        //private double P = 1;
        //private double LastMeasurement = 0;
        //private double K;

        //private void DoMeasurementUpdate()
        //{
        //    K = (P + Q) / (P + Q + R);
        //    P = R * (P + Q) / (R + P + Q);
        //}

        //public void SetStartingMeasurement(double startingMeasurement)
        //{
        //    LastMeasurement = 0;// startingMeasurement;
        //}

        //public double Update(double measurement)
        //{
        //    DoMeasurementUpdate();
        //    double result = LastMeasurement + (measurement - LastMeasurement) * K;
        //    LastMeasurement = result;
        //    return result;
        //}

        double A = double.Parse("1"); //factor of real value to previous real value
                                      // double B = 0; //factor of real value to real control signal
        double H = double.Parse("1");
        double P = double.Parse("0.1");
        double Q = double.Parse("0.125");  //Process noise. 
        double R = double.Parse("1"); //assumed environment noise.
        double K;
        double z;
        double x;

        public KalmanFilter(double initial)
        {
            //assign to first measured value
            x = initial;
        }

        public double DoWork(double noisy)
        {            

            //get current measured value
            z = noisy;

            //time update - prediction
            x = A * x;
            P = A * P * A + Q;

            //measurement update - correction
            K = P * H / (H * P * H + R);
            x = x + K * (z - H * x);
            P = (1 - K * H) * P;
            
            //estimated value
            return x;
        }

    }
}
