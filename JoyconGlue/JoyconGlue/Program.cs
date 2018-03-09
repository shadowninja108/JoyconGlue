﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using vJoyInterfaceWrap;
using static vJoyInterfaceWrap.vJoy;

using SharpJoycon;
using SharpJoycon.Interfaces;
using static SharpJoycon.Interfaces.HardwareInterface;
using SharpJoycon.Interfaces.Joystick;

namespace JoyconGlue
{
    class Program
    {
        static vJoy joystick;
        static uint vjd;
        static List<NintendoController> controllers;
        static GluedJoycons joycons;

        static void Main(string[] args)
        {
            EnablevJoy();
            FindControllers();
            CreateGluedJoycon();
            vJoyLoop();
        }

        public static void EnablevJoy()
        {
            Console.WriteLine("Getting vJoy controller...");
            joystick = new vJoy();
            if (joystick.vJoyEnabled())
            {
                Console.WriteLine("vJoy enabled!");
                Console.WriteLine("vJoy ver: " + joystick.GetvJoyVersion());
                Console.WriteLine("Manufacturer: " + joystick.GetvJoyManufacturerString());
                Console.WriteLine("Product: " + joystick.GetvJoyProductString());
                Console.WriteLine("Serial No. : " + joystick.GetvJoySerialNumberString());
                Console.WriteLine("Searching for available joystick...");
                for (uint i = 1; i < 16; i++)
                {
                    Console.WriteLine($"Trying joystick {i}...");
                    var status = joystick.GetVJDStatus(i);
                    Console.WriteLine($"Joystick status: {status}");
                    if (new VjdStat[] { VjdStat.VJD_STAT_OWN, VjdStat.VJD_STAT_FREE }.Contains(status))// is it owned already or ready to own?
                    {
                        vjd = i;
                        break;
                    }
                }
                if (vjd != 0) // joystick id can never be 0, not using -1 to avoid casting
                {
                    Console.WriteLine("Attempting to acquire joystick...");
                    if (joystick.AcquireVJD(vjd))
                    {
                        Console.WriteLine("Success!");
                    }
                    else
                    {
                        Console.WriteLine("Failed to acquire joystick!");
                    }
                }
                else
                {
                    Console.WriteLine("No joysticks available!");
                }

            }
            else
            {
                Console.WriteLine("vJoy not enabled!");
            }
        }

        public static void FindControllers()
        {
            Console.WriteLine("Finding Nintendo controllers...");
            controllers = NintendoController.Discover();
        }

        public static void CreateGluedJoycon()
        {
            Console.WriteLine("Applying some glue...");
            NintendoController leftJoycon = null;
            NintendoController rightJoycon = null;
            foreach (NintendoController controller in controllers)
            {
                HardwareInterface hardware = controller.GetHardware();
                hardware.SetReportMode(0x3F); // normal HID mode (should help with SPI reads)
                hardware.SetVibration(true);
                hardware.SetIMU(true);
                hardware.SetPlayerLights(PlayerLightState.Player1);

                switch (hardware.GetControllerType())
                {
                    case ControllerType.LeftJoycon:
                        Console.WriteLine("Left Joycon detected.");
                        leftJoycon = controller;
                        break;
                    case ControllerType.RightJoycon:
                        Console.WriteLine("Right Joycon detected.");
                        rightJoycon = controller;
                        break;
                    default:
                        Console.WriteLine("Unrecognized device.");
                        break;
                }
                hardware.SetReportMode(0x30); // 60hz update mode
            }
            joycons = new GluedJoycons(leftJoycon, rightJoycon);
        }

        public static void vJoyLoop()
        {
            joystick.ResetVJD(vjd);
            Console.WriteLine("Starting update loop...");
            JoystickState iReport;
            while (true)
            {
                iReport = new JoystickState();
                joycons.Poll();

                //buttons
                iReport.Buttons = joycons.GetButtonData();

                //pov
                uint pov = 0x7FFFFFF0; // set all but the 4 least significant bits to 1
                pov = pov | ((uint)joycons.GetPov()) - 1;
                iReport.bHats = pov;

                //sticks
                InputJoystick.StickPos leftPos = joycons.GetStick(0);
                InputJoystick.StickPos rightPos = joycons.GetStick(1);
                iReport.AxisX = leftPos.x;
                iReport.AxisY = leftPos.y;
                iReport.AxisXRot = rightPos.x;
                iReport.AxisYRot = rightPos.y;

                joystick.UpdateVJD(vjd, ref iReport);
                Thread.Sleep((1 / 60) * 1000); // joycons update @ 60hz
            }
        }
    }
}