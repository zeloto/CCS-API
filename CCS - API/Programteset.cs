﻿using System;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using System.Threading;
using System.IO;
using Thorlabs.ccs.interop64;

namespace CCS___Absorption_Measurement
{
    public class Program
    {
        static void Main(string[] args)
        {

            //For the initialization, the resource name needs to be changed to the name of the connected device
            //The resource name has this format: USB0::0x1313::<product ID>::<serial number>::RAW"

            // Product IDs are:
            // 0x8081: CCS100
            // 0x8083: CCS125
            // 0x8085: CCS150
            // 0x8087: CCS175
            // 0x8089: CCS200
            string ProductID = "0x8089";

            //The serial number is printed on the CCS spectrometer, please insert the SN number starts with "M"
            string SerialNumber = "M00991523";

            

            //Set the location to save the spectrum data
            string FileLocation = "C:\\Users\\vidar\\Documents\\ccs200";


            //Initialization
            TLCCS ccsSeries;
            try
            {
                //Create the resource name
                
                string ResourceName = "USB2::0x1313::" + ProductID + "::" + SerialNumber + "::RAW";
                ccsSeries = new TLCCS(ResourceName, false, false);
                Console.WriteLine("{0} is initialized!", SerialNumber);
            }
            catch
            {     
                Console.WriteLine("Initialization failed. Please inspect:\n 1. If the READY LED is green. \n 2. If the serial number and the item number is correct.");
                Console.WriteLine("Press any key to exit");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("Set the integration time, the unit is microsecond\nThe value should be within the range from 0.01 ms to 60000 ms");
            // Read the user input
            string userInput = Console.ReadLine();
            // Declare a variable to store the IntegrationTime
            //Set the integration time, the unit is microsecond
            //The value should be within the range from 0.01 ms to 60000 ms
            double IntegrationTime;
            if (double.TryParse(userInput, out IntegrationTime))
            {
                Console.WriteLine($"You entered the number: {IntegrationTime}");
            }
            else
            {
                Console.WriteLine("Invalid input.");
                Console.WriteLine("Press any key to exit");
                Console.ReadKey();
                return;
            }
            //Evaluate if the integration time is within the range.
            //The unit is microsecond (ms).
            if (IntegrationTime < 0.01)
            {
                Console.WriteLine("Integration time {0} ms is too small. Integration time will be set to 0.01 ms.", IntegrationTime);
                IntegrationTime = 0.01;
            }
            else if (IntegrationTime > 60000)
            {
                Console.WriteLine("Integration time {0} ms is too large. Integration time will be set to 60000 ms.", IntegrationTime);
                IntegrationTime = 60000;
            }
            else Console.WriteLine("Integration time is set to {0} ms. ", IntegrationTime);

            //Set the integration time to CCS. The unit used in CCS is second.

            ccsSeries.setIntegrationTime(IntegrationTime * 0.001);
            Thread.Sleep(100);
            Console.WriteLine("Set the number of scans");
            string userInput2 = Console.ReadLine();
            int cycle;
            if (int.TryParse(userInput2, out cycle))
            {
                Console.WriteLine($"You entered the number: {cycle}");
            }
            else
            {
                Console.WriteLine("Invalid input.");
                Console.WriteLine("Press any key to exit");
                Console.ReadKey();
                return;
            }

            for (int a = 0; a < cycle; a++)
            {
                //Get the wavelength data
                double[] DataWavelength = new double[3648];
                short DataSet = 0;
                double MinWavelength = 0;
                double MaxWavelength = 0;
                ccsSeries.getWavelengthData(DataSet, DataWavelength, out MinWavelength, out MaxWavelength);

                //Measure the reference spectrum
                Console.WriteLine("start measurement of reference spectrum.");
                double[] RefIntensity = new double[3648];
        

                Console.WriteLine("Scan started.");
                ccsSeries.startScan();

                //Wait for the scan to finish.
                int status;
                ccsSeries.getDeviceStatus(out status);
                while (status != 17)
                {
                    ccsSeries.getDeviceStatus(out status);
                    Thread.Sleep(100);
                }
                //The scan finished. Get the reference spectrum data.
                ccsSeries.getScanData(RefIntensity);
                Console.WriteLine("Scan finished.");


         

                //Measurement with sample
                Console.WriteLine("start measurement of sample spectrum.");
                double[] SampleIntensity = new double[3648];
            
               
                Console.WriteLine("Scan started.");
                ccsSeries.startScan();

                //Wait for the scan to finish.
                int status2;
                ccsSeries.getDeviceStatus(out status2);
                while (status2 != 17)
                {
                    ccsSeries.getDeviceStatus(out status2);
                    Thread.Sleep(100);
                }
                //The scan finished. Get the sample spectrum data.
                ccsSeries.getScanData(SampleIntensity);
                Console.WriteLine("Scan finished.");
            

                //Calculate the absorption and optical density of the sample.
                //Formulas:
                //Absorption[%] = ((Reference Spectrum - Sample Spectrum) / Reference Spectrum) * 100
                //Optical density = - log_10 (Transmission) =~ - log_10 (1- Absorption)
                //Conditional Staments are necessary to prevent errors due to impossible mathematical operations.
                double[] Absorption = new double[3648];
                double[] OD = new double[3648];
                for (int i = 0; i<3647;i++)
                {
                    Absorption[i] = (RefIntensity[i] - SampleIntensity[i]) / RefIntensity[i] * 100;
                    if (double.IsNaN(Absorption[i]) || double.IsInfinity(Absorption[i]))
                        Absorption[i] = 0;
                
                    OD[i] = -Math.Log10(1 - (Absorption[i] / 100));
                    if (double.IsNaN(OD[i]) || double.IsInfinity(OD[i]))
                        OD[i] = 0;
                }
                //Plot the spectrum
                Program program = new Program();
            

                //Dispose the device
            

                //Save the spectrums
           
                program.SaveSpectrum(FileLocation,Absorption,OD,DataWavelength,RefIntensity,SampleIntensity);
            }
            ccsSeries.Dispose();
            Console.WriteLine("Press ANY KEY to exit.");
            Console.ReadKey();
            

        }


        
        /// <summary>
        ///Creating a new .TXT format file, and save the absorption, optical density and wavelength data to the file.
        ///If the file already exists, it will be overwritten.
        /// </summary>
        private void SaveSpectrum(string FileLocation,double[] DataAbsorption, double[] DataOD,double[] DataWavelength, double[] RefIntensity, double[] SampleIntensity)
        {
            string savedata = "RefIntensity SampleIntensity Absrption(%)  OD  Wavelength(nm)\n";
            
            for (int i = 0; i < 3647; i++)
            {
                savedata += Convert.ToString(RefIntensity[i]) + "  ";
                savedata += Convert.ToString(SampleIntensity[i]) + "  ";
                savedata += Convert.ToString(DataAbsorption[i]) + "  ";
                savedata += Convert.ToString(DataOD[i]) + "  ";
                savedata += Convert.ToString(DataWavelength[i]) + "\n";
            }
                        
            FileStream fs = new FileStream(FileLocation + "\\CCS Spectrum " + DateTime.Now.ToString("yyyyMMdd HHmmssfff")+".txt",FileMode.Create);
            StreamWriter sw= new StreamWriter(fs);
            sw.Write(savedata);
            sw.Close();
            fs.Close();
            Console.WriteLine("Absorption and OD are saved to " + FileLocation + " successfully!");

        }
    }
  
}
