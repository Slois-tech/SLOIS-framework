using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Slois
{

	public class Misc
	{
		[DllImport("KERNEL32")]
		private static extern bool QueryPerformanceCounter(out long lpPerformanceCount);

		[DllImport("Kernel32.dll")]
		private static extern bool QueryPerformanceFrequency(out long lpFrequency);

		static public void MicroDelay(int micro)
		{
			long beg;
			QueryPerformanceCounter(out beg);
			long freq;
			QueryPerformanceFrequency(out freq);
			long end = beg + micro * freq / 1000000;
			while (true)
			{
				long curr;
				QueryPerformanceCounter(out curr);
				if (curr >= end) break;
			}
		}
	}
}
