using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace NeuralaceMagnetic.Controls
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct UniversalRobotRealTimeTCPStatus
    {
        public int MessageSize;
        public double Time;

        public double Q_Target_1;
        public double Q_Target_2;
        public double Q_Target_3;
        public double Q_Target_4;
        public double Q_Target_5;
        public double Q_Target_6;

        public double QD_Target_1;
        public double QD_Target_2;
        public double QD_Target_3;
        public double QD_Target_4;
        public double QD_Target_5;
        public double QD_Target_6;

        public double QDD_Target_1;
        public double QDD_Target_2;
        public double QDD_Target_3;
        public double QDD_Target_4;
        public double QDD_Target_5;
        public double QDD_Target_6;

        public double I_Target_1;
        public double I_Target_2;
        public double I_Target_3;
        public double I_Target_4;
        public double I_Target_5;
        public double I_Target_6;

        public double M_Target_1;
        public double M_Target_2;
        public double M_Target_3;
        public double M_Target_4;
        public double M_Target_5;
        public double M_Target_6;

        public double Q_Actual_1;
        public double Q_Actual_2;
        public double Q_Actual_3;
        public double Q_Actual_4;
        public double Q_Actual_5;
        public double Q_Actual_6;

        public double QD_Actual_1;
        public double QD_Actual_2;
        public double QD_Actual_3;
        public double QD_Actual_4;
        public double QD_Actual_5;
        public double QD_Actual_6;

        public double I_Actual_1;
        public double I_Actual_2;
        public double I_Actual_3;
        public double I_Actual_4;
        public double I_Actual_5;
        public double I_Actual_6;

        public double I_Control_1;
        public double I_Control_2;
        public double I_Control_3;
        public double I_Control_4;
        public double I_Control_5;
        public double I_Control_6;

        public double ToolVectorActual_1;
        public double ToolVectorActual_2;
        public double ToolVectorActual_3;
        public double ToolVectorActual_4;
        public double ToolVectorActual_5;
        public double ToolVectorActual_6;

        public double TCPSpeedActual_1;
        public double TCPSpeedActual_2;
        public double TCPSpeedActual_3;
        public double TCPSpeedActual_4;
        public double TCPSpeedActual_5;
        public double TCPSpeedActual_6;

        public double TCPForce_1;
        public double TCPForce_2;
        public double TCPForce_3;
        public double TCPForce_4;
        public double TCPForce_5;
        public double TCPForce_6;

        public double ToolVectorTarget_1;
        public double ToolVectorTarget_2;
        public double ToolVectorTarget_3;
        public double ToolVectorTarget_4;
        public double ToolVectorTarget_5;
        public double ToolVectorTarget_6;

        public double TCPSpeedTarget_1;
        public double TCPSpeedTarget_2;
        public double TCPSpeedTarget_3;
        public double TCPSpeedTarget_4;
        public double TCPSpeedTarget_5;
        public double TCPSpeedTarget_6;

        public double DigitalInputBits;

        public double MotorTemperature_1;
        public double MotorTemperature_2;
        public double MotorTemperature_3;
        public double MotorTemperature_4;
        public double MotorTemperature_5;
        public double MotorTemperature_6;

        public double ControllerTimer;
        public double TestValue;
        public double RobotMode;

        public double JointMode_1;
        public double JointMode_2;
        public double JointMode_3;
        public double JointMode_4;
        public double JointMode_5;
        public double JointMode_6;

        public double SafetyMode;

        public double UNKNOWNVALUE_1;
        public double UNKNOWNVALUE_2;
        public double UNKNOWNVALUE_3;
        public double UNKNOWNVALUE_4;
        public double UNKNOWNVALUE_5;
        public double UNKNOWNVALUE_6;

        public double ToolAccelerometer_1;
        public double ToolAccelerometer_2;
        public double ToolAccelerometer_3;

        public double UNKNOWNVALUE_2_1;
        public double UNKNOWNVALUE_2_2;
        public double UNKNOWNVALUE_2_3;
        public double UNKNOWNVALUE_2_4;
        public double UNKNOWNVALUE_2_5;
        public double UNKNOWNVALUE_2_6;

        public double SpeedScaling;
        public double LinearMomentumNorm;

        public double UNKNOWNVALUE3;
        public double UNKNOWNVALUE4;

        public double VMain;
        public double VRobot;
        public double IRobot;

        public double VActual_1;
        public double VActual_2;
        public double VActual_3;
        public double VActual_4;
        public double VActual_5;
        public double VActual_6;

        public double DigitalOuputs;
        public double ProgramState;
    }
}
