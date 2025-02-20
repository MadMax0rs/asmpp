using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace asmpp
{
	public static class Registers
	{
		public enum RegisterSizes : byte
		{
			none,
			_8,
			_16,
			_32,
			_64
		}
		public static string[] _8Bit =
		{
		"ah", "al",
		"bh", "bl",
		"ch", "cl",
		"dh", "dl",
		"sih", "sil",
		"dih", "dil",
		"bph", "bpl",
		"sph", "spl",
		"r8h", "r8b",
		"r9h", "r9b",
		"r10h", "r10b",
		"r11h", "r11b",
		"r12h", "r12b",
		"r13h", "r13b",
		"r14h", "r14b",
		"r15h", "r15b"
	};
		public static string[] _16Bit =
		{
		"ax", "bx", "cx", "dx",
		"si", "di", "bp", "sp",
		"r8w", "r9w", "r10w", "r11w", "r12w", "r13w", "r14w", "r15w"
	};
		public static string[] _32Bit =
		{
		"eax", "ebx", "ecx", "edx",
		"esi", "edi", "ebp", "esp",
		"r8d", "r9d", "r10d", "r11d", "r12d", "r13d", "r14d", "r15d"
	};
		public static string[] _64Bit =
		{
		"rax", "rbx", "rcx", "rdx",
		"rsi", "rdi", "rbp", "rsp",
		"r8", "r9", "r10", "r11", "r12", "r13", "r14", "r15"
	};

		public static bool IsRegister(string str)
		{
			return
				_64Bit.Contains(str) ||
				_32Bit.Contains(str) ||
				_16Bit.Contains(str) ||
				_8Bit.Contains(str);
		}
		public static bool IsRegister(Token register)
		{
			return
				register.type == TokenType._8BitRegister ||
				register.type == TokenType._16BitRegister ||
				register.type == TokenType._32BitRegister ||
				register.type == TokenType._64BitRegister;
		}

		public static int NumBytes(Token register)
		{
			return
				register.type == TokenType._8BitRegister ? 1 :
				register.type == TokenType._16BitRegister ? 2 :
				register.type == TokenType._32BitRegister ? 4 :
				register.type == TokenType._64BitRegister ? 8 :
				throw new Exception("Error: Token is not a register type");
		}

		public static string RegisterConvert(Token reg, RegisterSizes convertFrom, RegisterSizes convertTo, List<string> vars = null)
		{
			switch (convertFrom)
			{
				case RegisterSizes._8:
					break;
				case RegisterSizes._16:
					break;
				case RegisterSizes._32:
					if (convertTo == RegisterSizes._16)
					{
						return _16Bit[Array.IndexOf(_32Bit, reg.value)];
					}
					break;
				case RegisterSizes._64:
					if (convertTo == RegisterSizes._32)
					{
						return _32Bit[Array.IndexOf(_64Bit, reg.value)];
					}
					break;
				default:
					if (vars == null)
					{
						throw new Exception("Error: no vars given");
					}
					if (!vars.Contains(reg.value))
					{
						throw new Exception($"Error at {reg.line}:{reg.start}: Unknown symbol {reg.value}");
					}
					return reg.value;
			}
			throw new Exception("Error: unknown register size");
		}
	}

}
