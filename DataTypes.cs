using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using asmpp;

namespace asmpp
{
	public enum SectionType
	{
		none,
		data, // .data
		vars, // .bss
		prgm, // .text
		label
	}
	public enum CommandLineOptions : uint
	{
		showStacktrace = 0b_0001,
		entryPointDefined = 0b_0010,
		x64 = 0b_0100
	}
	public enum TokenType : int
	{
		/// <summary>
		/// Default if can't find a valid type
		/// </summary>
		none,
		/// <summary>
		/// External function definition
		/// </summary>
		_extern,
		/// <summary>
		/// Return
		/// </summary>
		_return,
		/// <summary>
		/// Name of section(prgm, statics, vars), expects '{' after secction name
		/// </summary>
		section,
		/// <summary>
		/// Integer literal
		/// </summary>
		int_lit,
		/// <summary>
		/// Name of the function being called
		/// </summary>
		funcCall,
		/// <summary>
		/// Function Definition
		/// </summary>
		label,
		/// <summary>
		/// allows defining of global labels
		/// </summary>
		global,
		/// <summary>
		/// String literal
		/// </summary>
		str_lit,
		/// <summary>
		/// ¯\_(ツ)_/¯ (unused)
		/// </summary>
		set,
		/// <summary>
		/// (
		/// </summary>
		argsStart,
		/// <summary>
		/// )
		/// </summary>
		argsEnd,
		/// <summary>
		/// Arguments when calling a function
		/// </summary>
		arg,
		/// <summary>
		/// Semicolon(;)
		/// </summary>
		semi,
		/// <summary>
		/// Default for unhandled keyword
		/// </summary>
		keyword,
		/// <summary>
		/// Chars that hold no real purpose for the final compilation, but could still cause errors if used incorrectly
		/// </summary>
		ignoreChar,
		/// <summary>
		/// =, +=, -=, *=, /=, &=, |=, ^=, ++, --, <<, >>
		/// </summary>
		_operator,  // TODO: Implement &=, |=, ^=, ++, --, <<, >>, !
		/// <summary>
		/// al, ah, bl, ah, etc.
		/// </summary>
		_8BitRegister,
		/// <summary>
		/// ax, bx, cx, dx, etc.
		/// </summary>
		_16BitRegister,
		/// <summary>
		/// eax, ebx, ecx, edx, etc.
		/// </summary>
		_32BitRegister,
		/// <summary>
		/// rax, rbx, rcx, rdx, etc.
		/// </summary>
		_64BitRegister,
		/// <summary>
		/// byte, word, dword, qword, etc.
		/// </summary>
		sizedType,
		/// <summary>
		/// an address for a location in memory, but not the memory at the address
		/// </summary>
		memoryAddress,
		/// <summary>
		/// the memory at a specified location
		/// </summary>
		memoryReference
		// TODO: Implement Comments
	}


	public struct Register
	{
		public string name;

		/// <summary>
		/// The register can be addressed by the name or the nickname
		/// </summary>
		public string nickname;

		/// <summary>
		/// Register size in bits
		/// </summary>
		public int size;

		public Register(string name, int size)
		{
			this.name = name;
			this.nickname = name;
			this.size = size;
		}
	}
	public struct Var
	{
		/// <summary>
		/// name of the variable
		/// </summary>
		public string name;
		/// <summary>
		/// defined size in bytes
		/// </summary>
		public uint sizeBytes;
		/// <summary>
		/// defined size in unit is was defined with
		/// </summary>
		public uint size;
		/// <summary>
		/// Used when value is a memory reference, otherwise its null
		/// </summary>
		public string sizeType;
		/// <summary>
		/// unit used to define var size
		/// </summary>
		public string unit;
		public Var(string name, uint sizeBytes, uint size, string unit, string sizeType = "")
		{
			this.name = name;
			this.sizeBytes = sizeBytes;
			this.size = size;
			this.unit = unit;
			this.sizeType = sizeType;
		}
	}
	public struct Token
	{
		public TokenType type;
		/// <summary>
		/// string of what is typed in asmpp
		/// </summary>
		public string value;
		/// <summary>
		/// byte, word, dword, etc.
		/// Used when value is a memory reference, otherwise its null
		/// </summary>
		public string sizeType;
		/// <summary>
		/// if applicable, the size in bytes(memory location, register, etc.)
		/// </summary>
		public uint? size;
		/// <summary>
		/// if applicable, the size in bits(memory location, register, etc.)
		/// </summary>
		public uint? sizeBits;
		/// <summary>
		/// how many characters after the start of the line is this token's first character
		/// </summary>
		public int start;
		/// <summary>
		/// how many characters after the start of the line is this token's last character
		/// </summary>
		public int end;
		/// <summary>
		/// the line # in the asmpp document
		/// </summary>
		public int line;

		public Token(TokenType type = TokenType.none, string value = "", string sizeType = "", uint? size = null, int start = 0, int end = 0, int line = 0)
		{
			this.type = type;
			this.value = value;
			this.size = size;
			this.start = start;
			this.end = end;
			this.line = line;
			this.sizeType = sizeType;
		}
	}
}
