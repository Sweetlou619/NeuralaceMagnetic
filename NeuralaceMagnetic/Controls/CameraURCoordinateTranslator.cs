using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace NeuralaceMagnetic.Controls
{
    public class CameraURCoordinateTranslator
    {
        double m_BaseRotation = 0;
        double m_ZBaseRotation = 0;
        public enum Direction
        {
            Forward,
            Backward,
            Right,
            Left,
            Up,
            Down
        }

        public void SetBaseRotation(double radians, double zradians)
        {
            m_BaseRotation = radians;
            m_ZBaseRotation = zradians;
        }

        public void GetPointWithBaseRoation(double x, double y, ref double outx, ref double outy)
        {
            double s = Math.Sin(m_BaseRotation);
            double c = Math.Cos(m_BaseRotation);

            outx = x * c - y * s;
            outy = x * s + y * c;
        }

        public void RotateFunction(double x, double y, double roation, ref double outx, ref double outy)
        {
            double s = Math.Sin(roation);
            double c = Math.Cos(roation);

            outx = x * c - y * s;
            outy = x * s + y * c;
        }

        public void GetPointWithBaseRoationZ(double x, double z, ref double outx, ref double outz)
        {
            if (m_ZBaseRotation == 0)
            {
                outz = z;
                outx = x;
                return;
            }

            double outx2 = 0;
            double outz2 = 0;

            RotateFunction(x, z, m_ZBaseRotation, ref outx2, ref outz2);

            outz = -outz2;
            outx = outx2;

            //double s = Math.Sin(m_ZBaseRotation);
            //double c = Math.Cos(m_ZBaseRotation);
            //outz = z * s;
            //outz = z * s + x * c;

            //from xy
            //outz = z * c - x * s;
            //outz = z * s + x * c;
        }

        public Point3D GetPointWithBaseRoation(Point3D point)
        {
            double outx = 0;
            double outy = 0;

            double outxFromZ = 0;
            double outz = 0;

            GetPointWithBaseRoation(point.X, point.Y, ref outx, ref outy);
            GetPointWithBaseRoationZ(outx, point.Z, ref outxFromZ, ref outz);
            Point3D baseRotated = new Point3D(outxFromZ, outy, outz);
            return baseRotated;
        }

        //public Point3D GetPointWithBaseRotationRelativeToolRoation(Point3D startpoint, Vector3D lookDirection, double moveM, Direction eDirection)
        //{
        //    Point3D baseRotated = GetPointWithBaseRoation(startpoint);
        //    return GetPointRelativeToolRoation(baseRotated, lookDirection, moveM, eDirection);
        //}

        public Point3D GetPointRelativeToolRoation(Point3D startpoint, Vector3D lookDirection, double moveM, Direction eDirection)
        {
            Vector3D rotationMatrix = MakeRotationDir(lookDirection, eDirection);
            Point3D output = startpoint + (rotationMatrix * moveM);
            return output;
        }


        private double DegreeToRadian(double angle)
        {
            return Math.PI * angle / 180.0;
        }

        public Vector3D ToRollPitchYawV2(Vector3D d)
        {
            double rx = d.X;
            double ry = d.Y;
            double rz = d.Z;

            double theta = Math.Sqrt(rx * rx + ry * ry + rz * rz);
            double kx = rx / theta;
            double ky = ry / theta;
            double kz = rz / theta;
            double cth = Math.Cos(theta);
            double sth = Math.Sin(theta);
            double vth = 1 - Math.Cos(theta);


            double r11 = kx * kx * vth + cth;
            double r12 = kx * ky * vth - kz * sth;
            double r13 = kx * kz * vth + ky * sth;
            double r21 = kx * ky * vth + kz * sth;
            double r22 = ky * ky * vth + cth;
            double r23 = ky * kz * vth - kx * sth;
            double r31 = kx * kz * vth - ky * sth;
            double r32 = ky * kz * vth + kx * sth;
            double r33 = kz * kz * vth + cth;


            double beta = Math.Atan2(-r31, Math.Sqrt(r11 * r11 + r21 * r21));
            double alpha;
            double gamma;

            if (beta > DegreeToRadian(89.99))
            {
                beta = DegreeToRadian(89.99);
                alpha = 0;
                gamma = Math.Atan2(r12, r22);
            }
            else if (beta < -DegreeToRadian(89.99))
            {
                beta = -DegreeToRadian(89.99);
                alpha = 0;
                gamma = -Math.Atan2(r12, r22);
            }
            else
            {
                double cb = Math.Cos(beta);
                alpha = Math.Atan2(r21 / cb, r11 / cb);
                gamma = Math.Atan2(r32 / cb, r33 / cb);
            }

            Vector3D rpy = new Vector3D();
            rpy.X = gamma;
            rpy.Y = beta;
            rpy.Z = alpha;
            return rpy;
        }

        double radtoang(double rad, double angleOffset = 0)
        {
            double ang = rad * (180 / Math.PI);
            ang += angleOffset;
            if (ang > 360)
            {
                while (ang > 360)
                {
                    ang -= 360;
                }
            }
            if (ang < -360)
            {
                while (ang < -360)
                {
                    ang += 360;
                }
            }
            return ang;
        }

        Vector3D ConvertEulerToDir(Vector3D inV)
        {
            Vector3D pry = ToRollPitchYawV2(inV);
            double pitch = radtoang(pry.X, 90);
            double roll = radtoang(pry.Y);
            double yaw = radtoang(pry.Z, 90);

            pitch = DegreeToRadian(pitch);
            roll = DegreeToRadian(roll);
            yaw = DegreeToRadian(yaw);

            Vector3D outV = new Vector3D();

            outV.X = Math.Cos(pitch) * Math.Cos(yaw);
            outV.Y = Math.Cos(pitch) * Math.Sin(yaw);
            outV.Z = Math.Sin(pitch);

            return outV;
        }

        //bool ShouldFlipY(Vector3D inV)
        //{
        //    Vector3D pry = ToRollPitchYawV2(inV);
        //    double pitch = radtoang(pry.X, 90);
        //    double roll = radtoang(pry.Y);
        //    double yaw = radtoang(pry.Z, 90);

        //    return (pitch > 180);
        //}

        Vector3D MakeRotationDir(Vector3D euler, Direction eDirection)
        {
            Vector3D direction = ConvertEulerToDir(euler);
            direction.Normalize();

            Vector3D up = new Vector3D(0, 0, 1);

            Vector3D xaxis = Vector3D.CrossProduct(up, direction);
            xaxis.Normalize();

            Vector3D yaxis = Vector3D.CrossProduct(direction, xaxis);
            yaxis.Normalize();

            //bool flipY = ShouldFlipY(euler);

            if (eDirection == Direction.Up)
            {
                return xaxis;
            }
            else if (eDirection == Direction.Down)
            {
                return -xaxis;
            }
            else if (eDirection == Direction.Right)
            {
                //if (flipY)
                //{
                //    return -yaxis;
                //}
                return yaxis;
            }
            else if (eDirection == Direction.Left)
            {
                //if (flipY)
                //{
                //    return yaxis;
                //}
                return -yaxis;
            }
            else if (eDirection == Direction.Backward)
            {
                return -direction;
            }
            else if (eDirection == Direction.Forward)
            {
                return direction;
            }

            return new Vector3D();
        }

        public Vector3D QuaternionToRPY(double x, double y, double z, double w)
        {
            double heading = Math.Atan2(2 * y * w - 2 * x * z, 1 - 2 * Math.Pow(y, 2) - 2 * Math.Pow(z, 2));
            double attitude = Math.Asin(2 * x * y + 2 * z * w);
            double bank = Math.Atan2(2 * x * w - 2 * y * z, 1 - 2 * Math.Pow(x, 2) - 2 * Math.Pow(z, 2));

            if ((x * y + z * w) == 0.5)
            {
                heading = 2 * Math.Atan2(x, w);
                bank = 0;
            }
            if ((x * y + z * w) == -0.5)
            {
                heading = -2 * Math.Atan2(x, w);
                bank = 0;
            }
            Vector3D returnVector = new Vector3D(heading, attitude, bank);
            return returnVector;
        }
    }
}
