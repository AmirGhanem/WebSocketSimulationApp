using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Fleck;

class Program
{
    static List<IWebSocketConnection> allClients = new List<IWebSocketConnection>();

    static void Main(string[] args)
    {
        Console.WriteLine("🚀 Starting WebSocket Server on ws://10.0.20.36:8080");
        var server = new WebSocketServer("ws://10.0.20.36:8080");

        server.Start(socket =>
        {
            socket.OnOpen = () =>
            {
                try
                {
                    Console.WriteLine($"✅ Client connected: {socket.ConnectionInfo.ClientIpAddress}");
                    allClients.Add(socket);
                   
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ OnOpen Error: {ex}");
                }
            };

            // Handle text messages
            socket.OnMessage = message =>
            {
                try
                {
                    Console.WriteLine($"📩 Received text: {message}");
                    BroadcastText(message, socket);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ OnMessage Error: {ex}");
                }
            };

            // New handler for binary data
            List<byte> audioBuffer = new List<byte>();
            int targetSizeBytes = 35 * 1024; // 1 second file
            int sampleRate = 16000;
            int bitsPerSample = 16;
            int channels = 1;

            socket.OnBinary = bytes =>
            {
                try
                {
                    // Add new data to buffer
                    audioBuffer.AddRange(bytes);

                    // Save when buffer reaches target size
                    if (audioBuffer.Count >= targetSizeBytes)
                    {
                        SaveAudioChunk();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ OnBinary Error: {ex}");
                }
            };
            void SaveAudioChunk()
            {
                var bufferArray = audioBuffer.ToArray();
                var header = CreateWavHeader(bufferArray.Length, sampleRate, bitsPerSample, channels);
                var wavData = header.Concat(bufferArray).ToArray();

                File.WriteAllBytes($"audio_{DateTime.Now.Ticks}.wav", wavData);
                Console.WriteLine($"Saved {wavData.Length} byte file ({bufferArray.Length} PCM bytes)");

                audioBuffer.Clear();
            }
            socket.OnClose = () =>
            {
                try
                {
                    Console.WriteLine("❌ Client disconnected");
                    allClients.Remove(socket);
                    if (audioBuffer.Count > 0)
                    {
                        SaveAudioChunk();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ OnClose Error: {ex}");
                }
            };
        });

        Console.WriteLine("✅ Server running. Press ENTER to exit.");
        Console.ReadLine();
    }

    static void BroadcastText(string message, IWebSocketConnection sender)
    {
        foreach (var client in allClients.ToList())
        {
            if (client != sender && client.IsAvailable)
            {
                client.Send(message);
            }
        }
    }

    static void BroadcastBinary(byte[] data, IWebSocketConnection sender)
    {
        foreach (var client in allClients.ToList())
        {
            if (client != sender && client.IsAvailable)
            {
                client.Send(data);
            }
        }
    }
    private static byte[] CreateWavHeader(int dataLength, int sampleRate, int bitsPerSample, int channels)
    {
        // WAV header specification
        var header = new List<byte>();

        // RIFF header
        header.AddRange(Encoding.ASCII.GetBytes("RIFF"));
        header.AddRange(BitConverter.GetBytes(dataLength + 36)); // Chunk size
        header.AddRange(Encoding.ASCII.GetBytes("WAVE"));

        // fmt subchunk
        header.AddRange(Encoding.ASCII.GetBytes("fmt "));
        header.AddRange(BitConverter.GetBytes(16));          // Subchunk size
        header.AddRange(BitConverter.GetBytes((short)1));    // Audio format (PCM)
        header.AddRange(BitConverter.GetBytes((short)channels));
        header.AddRange(BitConverter.GetBytes(sampleRate));
        header.AddRange(BitConverter.GetBytes(sampleRate * channels * (bitsPerSample / 8))); // Byte rate
        header.AddRange(BitConverter.GetBytes((short)(channels * (bitsPerSample / 8))));      // Block align
        header.AddRange(BitConverter.GetBytes((short)bitsPerSample));

        // data subchunk
        header.AddRange(Encoding.ASCII.GetBytes("data"));
        header.AddRange(BitConverter.GetBytes(dataLength));

        return header.ToArray();
    }
}