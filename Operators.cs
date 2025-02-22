using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;

namespace asmpp
{
	public class Operators
	{
		private enum operandType : byte
		{
			unknownSymbol,
			register,
			/// <summary>
			/// a location in memory, but not the memory at the location
			/// </summary>
			memoryAddress,
			/// <summary>
			/// the memory at a specified location
			/// </summary>
			memoryReference,
			/// <summary>
			/// integer litteral
			/// </summary>
			int_lit
		}

		// https://en.wikipedia.org/wiki/X86_instruction_listings

		public static string MovAddSub(Token left, Token right, string operation)
		{
			// 0 = unknown symbol, 1 = register, 2 = memory location, 3 = memory reference
			operandType aType =
				left.type == TokenType.memoryReference ? operandType.memoryReference :
				left.type == TokenType.memoryAddress ?
				// If left hand operand is a memory location
				throw new Exception($"Error at {left.line}:{left.line}: Left hand parameter cannot be a memory location") :
				Registers.IsRegister(left.value) ? operandType.register :
				int.TryParse(left.value, out int _) ?
				// Left hand parameter is an inager literal
				throw new Exception($"Error at {left.line}:{left.start}: Left hand parameter cannot be an integer literal") :
				// If left param is an unknown symbol
				throw new Exception($"Error at {left.line}:{left.start}: Unknown symbol {left.value}");

			operandType bType =
				right.type == TokenType.memoryReference ? operandType.memoryReference :
				right.type == TokenType.memoryAddress ? operandType.memoryAddress :
				Registers.IsRegister(right.value) ? operandType.register :
				int.TryParse(right.value, out int _) ? operandType.int_lit :
				// Right hand parameter is not a register, integer literal or a reserved space in memory
				throw new Exception($"Error at {right.line}:{right.start}: Unknown symbol {right.value}");

			// Cannot move from one memory location directly to another
			if ((aType == operandType.memoryReference) && (bType == operandType.memoryReference))
			{
				// Both operands are memory addresses
				throw new Exception($"Error at {right.line}:{right.start}: Both operands connot be memory locations");
			}
			// Both operands must be the same size in bytes
			else if (left.size != Registers.NumBytes(right))
			{
				throw new Exception($"Error at {right.line}:{right.start}: size of right hand operand must match the left");
			}
			if ((bType == operandType.int_lit && (uint)Math.Pow(2, (uint)left.sizeBits) < uint.Parse(right.value)) || (bType != operandType.int_lit && left.sizeBits != right.sizeBits))
			{
				throw new Exception($"Error at {left.line}:{left.start}: Size missmatch between '{left.value}' and '{right.value}'");
			}

			string output = $"{operation} ";
			// Operation is good and passed all checks

			output += aType switch
			{
				// Left hand operand is a register
				// Left hand operand is a memory address
				operandType.register or operandType.memoryAddress
					=> $"{right.value}, ",

				// Left hand operand is a reference to piece of memory
				operandType.memoryReference
					=> $"{right.sizeType}[{right.value}], ",

				// Default
				_ => throw new Exception("Error: Unknown operand type"),
			};
			output += bType switch
			{
				// Right hand operand is an intager litteral
				// Right hand operand is a register
				// Right hand operand is a memory address
				operandType.int_lit or operandType.register or operandType.memoryAddress
					=> right.value,

				// Right hand operand is a reference to piece of memory
				operandType.memoryReference
					=> $"[{right.value}]",

				// Default
				_ => throw new Exception("Error: Unknown operand type"),
			};
			return output;
		}
		
		// mov, add, and sub can have same operands, only difference is the operator
		public static string Mov(Token left, Token right)
		{
			return MovAddSub(left, right, "mov");
		}
		public static string Add(Token left, Token right)
		{
			return MovAddSub(left, right, "add");
		}
		public static string Sub(Token left, Token right)
		{
			return MovAddSub(left, right, "sub");
		}
	
		
		public static string DivMul(Token operand, string operation)
		{
			if (operand.type == TokenType.memoryAddress)
			{
				throw new Exception($"Error at {operand.line}:{operand.start}: operand cannot be a memory address");
			}

			string output = $"{operation} ";

			output += operand.type switch
			{

			}
		}
	}
}
