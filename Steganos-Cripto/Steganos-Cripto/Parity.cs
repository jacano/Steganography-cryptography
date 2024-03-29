﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Collections;

namespace Steganos_Cripto
{
    class Parity : Algorithm
    {
        public Parity()
        {
            base.EncryptView = new ParityEncryptControl();
            base.DecryptView = new ParityDecryptControl();

            base.Name = "Parity Coding";
        }

        public override void update()
        {
            int numSamples = WavProcessor.numSamples(State.Instance.FileNameIn);

            int samplesPerRegionEncrypt = State.Instance.SamplesPerRegionParityEncrypt;
            int numRegionsEncrypt = numSamples / samplesPerRegionEncrypt;
            State.Instance.MaxMessageLengthParityEncrypt = numRegionsEncrypt / 8;

            int samplesPerRegionDecrypt = State.Instance.SamplesPerRegionParityDecrypt;
            int numRegionsDecrypt = numSamples / samplesPerRegionDecrypt;
            State.Instance.MaxMessageLengthParityDecrypt = numRegionsDecrypt / 8;
        }

        public override bool encrypt(String message, String key)
        {
            int samplesPerRegion = State.Instance.SamplesPerRegionParityEncrypt;
            int seed = State.Instance.SeedParityEncrypt;

            WavProcessor wProcessor = new WavProcessor(State.Instance.FileNameIn);
            Header header = wProcessor.header;
            IList<Sample[]> regions = getRegions(wProcessor.samples, samplesPerRegion);

            if (message.Length > State.Instance.MaxMessageLengthParityEncrypt)
            {
                return false;
            }

            IndexRandomGenerator rnd = new IndexRandomGenerator(seed, regions.Count);
            Random rnd2 = new Random((int)DateTime.Now.Ticks);

            byte[] messageBytes = null;
            if (key.Equals(""))
            {
                messageBytes = Encoding.ASCII.GetBytes(message);
            }
            else
            {
                messageBytes = Xor.XorMessageWithKey(Encoding.ASCII.GetBytes(message), key);
            }

            BitArray xoredMessageArray = new BitArray(messageBytes);

            int messageBitArrayIndex = 0;
            while (messageBitArrayIndex < xoredMessageArray.Length)
            {
                int regionIndex = rnd.generateUnusedIndex();

                Sample[] s1 = regions[regionIndex];

                bool parity = CalculateParity(s1);

                if(parity != xoredMessageArray[messageBitArrayIndex])
                {
                    int sampleIndex = rnd2.Next(s1.Length);
                    regions[regionIndex][sampleIndex].data[State.Instance.BitsPerSample - 1] = !regions[regionIndex][sampleIndex].data[State.Instance.BitsPerSample - 1];
                }  
            
                messageBitArrayIndex++;
            }

            IList<Sample> outputSamples = new List<Sample>();
            foreach (Sample[] ss in regions)
            {
                foreach (Sample s in ss)
                {
                    outputSamples.Add(s);
                }
            }

            WavWriter.run(State.Instance.FileNameOut, header, outputSamples.ToArray<Sample>());

            return true;
        }


        public override String decrypt(String key)
        {
            int samplesPerRegion = State.Instance.SamplesPerRegionParityDecrypt;
            int messageLength = State.Instance.MessageLengthParityDecrypt;
            int seed = State.Instance.SeedParityDecrypt;

            WavProcessor wProcessor = new WavProcessor(State.Instance.FileNameIn);
            IList<Sample[]> regions = getRegions(wProcessor.samples, samplesPerRegion);

            if (messageLength > State.Instance.MaxMessageLengthParityDecrypt)
            {
                return null;
            }

            IndexRandomGenerator rnd = new IndexRandomGenerator(seed, regions.Count);

            int size = messageLength * 8;
            BitArray xoredMessageArray = new BitArray(size);

            int bitCount = 0;
            while (bitCount < size)
            {
                int regionIndex = rnd.generateUnusedIndex();

                Sample[] s1 = regions[regionIndex];

                bool parity = CalculateParity(s1);

                xoredMessageArray[bitCount++] = parity;
            }

            byte[] messageXor = Util.ToByteArray(xoredMessageArray);
            for (int i = 0; i < messageXor.Length; i++) messageXor[i] = BitReverser.Reverse(messageXor[i]);
            string res = "";

            if (!key.Equals(""))
            {
                res = Encoding.ASCII.GetString(Xor.XorMessageWithKey(messageXor, key));
            }
            else
            {
                res = Encoding.ASCII.GetString(messageXor);
            }

            return res;
        }

        #region Helpers
        private IList<Sample[]> getRegions(Sample[] samples, int samplesPerRegion)
        {
            int numRegions = samples.Length / samplesPerRegion;

            IList<Sample[]> regions = new List<Sample[]>();

            int j = 0;
            for (int t = 0; t < numRegions; t++)
            {
                Sample[] s = new Sample[samplesPerRegion];

                for (int i = 0; i < s.Length; i++)
                {
                    s[i] = samples[j++];
                }
                regions.Add(s);
            }

            int rest = samples.Length % numRegions;

            if (rest != 0)
            {
                Sample[] s = new Sample[rest];
                for (int i = 0; i < s.Length; i++)
                {
                    s[i] = samples[j++];
                }
                regions.Add(s);
            }

            return regions;
        }
        private bool CalculateParity(Sample[] s1)
        {
            bool res = false;

            foreach (Sample s in s1)
            {
                foreach (bool b in s.data)
                {
                    res ^= b;
                }
            }

            return res;
        }
#endregion
    }
}
