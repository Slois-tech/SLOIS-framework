using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Slois
{
	public class NeuroNet
	{
		public delegate bool FuncStat(Stat st);

		internal class Example
		{
			internal float[] inputs;
			internal float output;
		}

		public class Stat
		{
			public int state; //0 - обучение, 1 - обучение закончилось
			public int epoch;
			public float exact;
		}

		List<Example> examples;
		float[] C;

		public NeuroNet()
		{
			examples = new List<Example>();
		}

		public void AddExample(float[] inputs, float output)
		{
			Example e = new Example();
			e.inputs = inputs;
			e.output = output;
			examples.Add(e);
		}

		public float Train(float exact, int maxEpochs, FuncStat fs, out int passEpoch)
		{
			Stat st = new Stat();
			C = new float[examples[0].inputs.Length];
			float[] sumi = new float[examples.Count];
			for (int i = 0; i < examples.Count; i++)
			{
				sumi[i] = 0;
				for (int j = 0; j < examples[i].inputs.Length; j++)
					sumi[i] += examples[i].inputs[j] * examples[i].inputs[j];
			}
			int epoch = 0;
			bool trained = true;
			while (epoch < maxEpochs && trained)
			{
				trained = false;
				float sumdo = 0;
				for (int i = 0; i < examples.Count; i++)
				{
					float o = Execute(examples[i].inputs);
					float d_o = examples[i].output - o;
					sumdo += Math.Abs(d_o);
					if (Math.Abs(d_o) > exact)
					{
						for (int j = 0; j < C.Length; j++)
						{
							C[j] += (d_o * examples[i].inputs[j]) / sumi[i];
						}
						trained = true;
					}
				}
				st.state = 0;
				st.epoch = epoch;
				st.exact = sumdo / examples.Count;
				if (fs(st)) break;
				epoch++;
			}
			passEpoch = epoch;
			return st.exact;
		}

		public float Execute(float[] inputs)
		{
			float ret = 0;
			for (int i = 0; i < C.Length; i++)
				ret += C[i] * inputs[i];
			return ret;
		}

		public bool Load(System.IO.StreamReader sr)
		{
			string count_s = sr.ReadLine();
			int count = int.Parse(count_s);
			C = new float[count];
			for (int i = 0; i < count; i++)
			{
				string s = sr.ReadLine();
				C[i] = float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
			}
			return true;
		}

		public void Save(System.IO.StreamWriter sw)
		{
			sw.WriteLine(C.Length);
			for(int i = 0; i < C.Length; i++)
				sw.WriteLine(C[i].ToString(System.Globalization.CultureInfo.InvariantCulture));
		}
	}
}
