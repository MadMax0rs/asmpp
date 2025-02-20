using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace asmpp
{
	public static class Consts
	{
		public static readonly string[] TokenTypeStrings = { "\0", "extern", "ret", "section", "integer literal", "call", ":", "global", "\"", "=", "(", ")", ",", ";", "keyword", "", "=, +=, -=, *=, etc.", "al, ah, bl, ah, etc.", "ax, bx, cx, dx, etc.", "eax, ebx, ecx, edx, etc.", "rax, rbx, rcx, rdx, etc." };
		public static readonly string[] SizedTypes =
		{
			"byte",		// 8-Bit,	1-Byte
			"word",		// 16-Bit,	2-Byte
			"dword",	// 32-Bit,	4-Byte
			"qword",	// 64-Bit,	8-Byte
			"tword",	// 80-Bit,	10-Byte
			"oword",	// 128-Bit,	16-Byte
			"yword",	// 256-Bit,	32-Byte
			"zword"		// 512-Bit,	64-Byte
	};
		public static readonly string[] SizedTypesBss =
		{
			"resb",		// 8-Bit,	1-Byte
			"resw",		// 16-Bit,	2-Byte
			"resd",		// 32-Bit,	4-Byte
			"resq",		// 64-Bit,	8-Byte
			"rest",		// 80-Bit,	10-Byte
			"reso",		// 128-Bit,	16-Byte
			"resy",		// 256-Bit,	32-Byte
			"resz"		// 512-Bit,	64-Byte
	};
		public static int NumBytes(string unit, int num)
		{
			int index = Array.IndexOf(Consts.SizedTypes, unit);
			if (index < 4)
			{
				return (int)Math.Pow(2, index) * num;
			}
			else if (index == 4)
			{
				return 10 * num;
			}
			else
			{
				return (int)Math.Pow(2, index - 1) * num;
			}
		}
	}
}
