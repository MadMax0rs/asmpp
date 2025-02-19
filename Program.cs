// See https://aka.ms/new-console-template for more information
using System.Text;
using System.Collections;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.ComponentModel.DataAnnotations.Schema;

if (args.Length <= 0)
{
	Console.WriteLine("Error: no filePath given, type asmpp -? for help");
	return;
}

if (args[0] == "-?")
{
	Console.WriteLine("asmpp help:\nasmpp <File Path> \t\tcompile asmpp file\nasmpp -?\t\t print the asmpp help page");
	return;
}

string filePath = args[0];

if (!File.Exists(filePath))
{
	Console.WriteLine($"Error: File at path {filePath} does not exist");
	return;
}

string entryPoint = "";

uint selectedCommandLineOptions = 0b_0000;


// Start i at 1 to exclude the filepath
for (int i = 1; i < args.Length; i++)
{
	switch (args[i])
	{
		// Show stacktrace in error output
		case "-st":
			selectedCommandLineOptions |= (uint)CommandLineOptions.showStacktrace;
			break;
		case "-entry":
			i++;
			if (i == args.Length)
			{
				throw new Exception("Error in asmpp invocation: no entrypoint given, please use '-entry entryPointName' to define an entry point");
			}
			entryPoint = args[i];
			selectedCommandLineOptions |= (uint)CommandLineOptions.entryPointDefined;
			break;
		case "-x86":
			break;
		case "-x64":
			break;
		default:
			Console.WriteLine($"unrecognized command line argument {args[i]}");
			return;
	}
}
if ((selectedCommandLineOptions & (uint)CommandLineOptions.entryPointDefined) != (uint)CommandLineOptions.entryPointDefined)
{
	throw new Exception("Error in asmpp invocation: no entrypoint given, please use '-entry entryPointName' to define an entry point");
}


string FileString = File.ReadAllText(filePath);
List<Token> Tokens = [];
List<Var> Vars = [];
string Output = "";

List<Token> SplitString(string code)
{
	code = code.Replace("\r\n", "\n");
	string[] splitCode = Regex.Split(code, @"([\s'""{}\(\)\[\],;\\=*/=\-+<>])").Where(s => s != "").ToArray();
	List<Token> tokens = [];
	int line = 1;
	int column = 0;

	for (int i = 0; i < splitCode.Length; i++)
	{
		string str = splitCode[i];
		if (str == "\n")
		{
			line++;
			column = 0;
			continue;
		}
		Token currentToken = new Token(value:str,start:column,line:line);
		column += str.Length;
		currentToken.end = column;
		tokens.Add(currentToken);
	}

	return tokens;
}

int FindNextNonWhitespace(List<Token> tokensIn, int currentIndex)
{
	// <2 chars | Is whitespace | Output
	// ---------|---------------|-------
	// False    | False         | False
	// False    | True          | False, Error(Should not have whitespace as the first character of a token's value
	// True     | False         | False
	// True     | True          | True
	while (tokensIn[currentIndex].value.Length < 2 && char.IsWhiteSpace(tokensIn[currentIndex].value[0]))
	{
		currentIndex++;
	}
	return currentIndex;
}
int FindLastNonWhitespace(List<Token> tokensIn, int currentIndex)
{
	// <2 chars | Is whitespace | Output
	// ---------|---------------|-------
	// False    | False         | False
	// False    | True          | False, Error(Should not have whitespace as the first character of a token's value
	// True     | False         | False
	// True     | True          | True
	while (tokensIn[currentIndex].value.Length < 2 && char.IsWhiteSpace(tokensIn[currentIndex].value[0]))
	{
		currentIndex--;
	}
	return currentIndex;
}
bool IsRegister(string str)
{
	return
		Registers._64Bit.Contains(str) ||
		Registers._32Bit.Contains(str) ||
		Registers._16Bit.Contains(str) ||
		Registers._8Bit.Contains(str);
}
bool IsVar(string var)
{
	for (int i = 0; i < Vars.Count; i++)
	{
		if (var == Vars[i].name)
		{
			return true;
		}
	}
	return false;
}

List<Token> Tokenize(List<Token> tokensIn)
{
	List<Token> tokensOut = [];
	bool stringLiteral = false;
	bool escapeChar = false;
	bool arg = false;
	Token currentToken = new(start: 0, line: 0);
	int lastCheckedToken = -1;

	for (int i = 0; i < tokensIn.Count; i++)
	{
		if (tokensIn[i].value == "")
		{
			throw new Exception("Error: Why did I find a blank string... blank strings should've been removed... 😬😬😬");
		}

		if (stringLiteral)
		{
			if (tokensIn[i].value == @"[\\][rnts""]" && !escapeChar)
			{
				currentToken.value = Regex.Unescape(currentToken.value);
				tokensOut.Add(currentToken);
				stringLiteral = false;
				continue;
			}
			escapeChar = false;
			if (tokensIn[i].value.EndsWith('\\'))
			{
				escapeChar = true;
			}
			currentToken.value += tokensIn[i].value;
			currentToken.end = tokensIn[i].end;

			continue;
		}
		// Set current token after string literal so that, when dealing with a string literal, it keeps its value, start, and line
		currentToken = tokensIn[i];
		int next = 0;
		switch (tokensIn[i].value)
		{
			// Keywords
			case "extern":
				next = FindNextNonWhitespace(tokensIn, i + 1);
				currentToken = tokensIn[next];
				currentToken.type = TokenType._extern;
				tokensOut.Add(currentToken);
				i = next;
				break;

			case "return":
				next = FindNextNonWhitespace(tokensIn, i + 1);
				currentToken = tokensIn[next];
				currentToken.type = TokenType._return;
				tokensOut.Add(currentToken);
				i = next;
				break;

			case "section":
				next = FindNextNonWhitespace(tokensIn, i + 1);
				currentToken = tokensIn[next];
				currentToken.type = TokenType.section;
				tokensOut.Add(currentToken);
				i = next;
				break;

			case "global":
				currentToken.type = TokenType.global;
				tokensOut.Add(currentToken);
				break;
			case "label":
				next = FindNextNonWhitespace(tokensIn, i + 1);
				currentToken = tokensIn[next];
				currentToken.type = TokenType.label;
				tokensOut.Add(currentToken);
				i = next;
				break;

			case "(":
				currentToken.type = TokenType.argsStart;
				tokensOut.Add(currentToken);
				arg = true;
				int j = 1;
				while (tokensIn[i - j].value.Length < 1 || !char.IsWhiteSpace(tokensIn[i - j].value[0]))
				{
					if (i - j <= 0)
					{
						throw new Exception($"Error at symbol '(' at {currentToken.line}:{currentToken.start}: Unable to find function name to call");
					}
					j++;
				}
				j--;
				currentToken = tokensIn[i - j];
				currentToken.type = TokenType.funcCall;
				tokensOut.Insert(tokensOut.Count - 1, currentToken);
				break;

			case ")":
				if (tokensOut[tokensOut.Count - 1].type != TokenType.argsStart && tokensOut[tokensOut.Count - 1].type != TokenType.arg)
				{
					throw new Exception($"Error at {currentToken.line}:{currentToken.start}: Unexpected symbol ')'");
				}
				currentToken.type = TokenType.argsEnd;
				tokensOut.Add(currentToken);
				arg = false;
				break;

			case "[":
				Token lastKeyword = tokensIn[FindLastNonWhitespace(tokensIn, i - 1)];
				// If the last non-whitespace is a valid sized type(byte, word, etc.)
				if (Consts.SizedTypes.Contains(lastKeyword.value))
				{
					lastKeyword.type = TokenType.sizedType;
					tokensOut.Add(lastKeyword);

					int varIndex = FindNextNonWhitespace(tokensIn, i + 1);
					Token var = tokensIn[varIndex];

					var.type = TokenType.memoryReference;
					tokensOut.Add(var);

					i = FindNextNonWhitespace(tokensIn, varIndex + 1);
					if (tokensIn[i].value != "]")
					{
						throw new Exception($"Error at {tokensIn[i].line}:{tokensIn[i].start}: Expected ']' but got {tokensIn[i].value}");
					}
					i++;
				}
				else
				{
					throw new Exception($"Error at {lastKeyword.line}:{lastKeyword.start}: Expected a sized type(byte, word, dword, etc.) but got {lastKeyword.value}");
				}
				break;
			case "]":
				throw new Exception($"Error at {tokensIn[i].line}:{tokensIn[i].start}: Unexpected ']'");

			case ",":
				if (!arg)
				{
					throw new Exception($"Error at symbol ',' at {currentToken.line}:{currentToken.start}: Unexpected symbol ','");
				}

				break;

			// Semicolon(;) - End of line
			case ";":
				currentToken.type = TokenType.semi;
				tokensOut.Add(currentToken);
				break;

			// Double Quote(") - Start string literal
			case "\"":
				stringLiteral = true;
				currentToken.type = TokenType.str_lit;
				break;
			// Ingore Characters({}) - Chars that hold no real purpose for the final compilation, but could still cause errors if used incorrectly
			case "{":
			case "}":
				currentToken.type = TokenType.ignoreChar;
				tokensOut.Add(currentToken);
				break;
			// Whitespace characters
			case " ":
			case "\t":
			case "\n":
			case "\r":
			case "\v":
				continue;
			// Operators
			case "<":
				if (tokensIn[i + 1].value == "<")
				{
					i++;
					next = FindNextNonWhitespace(tokensIn, i);

				}
				break;
			case ">":
				if (tokensIn[i + 1].value == ">")
				{
					i++;
					next = FindNextNonWhitespace(tokensIn, i);

				}
				break;
			case "+":
				if (tokensIn[i + 1].value == "+")
				{
					i++;
					next = FindNextNonWhitespace(tokensIn, i);

				}
				break;
			case "-":
				if (tokensIn[i + 1].value == "-")
				{
					i++;
					next = FindNextNonWhitespace(tokensIn, i);

				}
				break;
			case "=":
				{
					if (i < 1)
					{
						throw new Exception($"Error at {currentToken.line}:{currentToken.start}: no specified register to write to");
					}

					j = 2;
					currentToken.type = TokenType._operator;
					if (tokensIn[i - 1].value == "*" || tokensIn[i - 1].value == "/" || tokensIn[i - 1].value == "-" || tokensIn[i - 1].value == "+")
					{
						currentToken.value = tokensIn[i - 1].value + currentToken.value;

					}
					else
					{
						
						j = 1;
					}

					Token tmp = tokensIn[FindLastNonWhitespace(tokensIn, i - j)];
					tmp.type =
						Registers. _8Bit.Contains(tmp.value) ? TokenType. _8BitRegister :
						Registers._16Bit.Contains(tmp.value) ? TokenType._16BitRegister :
						Registers._32Bit.Contains(tmp.value) ? TokenType._32BitRegister :
						Registers._64Bit.Contains(tmp.value) ? TokenType._64BitRegister :
						throw new Exception($"Error at {tmp.line}:{tmp.start}: Invalid register {tmp.value}");

					tokensOut.Add(currentToken);
					tokensOut.Add(tmp);
					currentToken = tokensIn[FindNextNonWhitespace(tokensIn, i + 1)];

					currentToken.type =
						Registers._8Bit.Contains(currentToken.value) ? TokenType._8BitRegister :
						Registers._16Bit.Contains(currentToken.value) ? TokenType._16BitRegister :
						Registers._32Bit.Contains(currentToken.value) ? TokenType._32BitRegister :
						Registers._64Bit.Contains(currentToken.value) ? TokenType._64BitRegister :
						int.TryParse(currentToken.value, out int _) ? TokenType.int_lit :	// Don't need the number from the parse, only the successfullness of the operation
						throw new Exception($"Error at {currentToken.line}:{currentToken.start}: Invalid register {currentToken.value}");

					tokensOut.Add(currentToken);
					i++;
					break;
				}
			default:
				if (!arg)
				{
					// token type is TokenType.none by default
					tokensOut.Add(currentToken);
					continue;
				}
				if (IsVar(currentToken.value))
				{
					currentToken.type = TokenType.memoryLocation;
					tokensOut.Add(currentToken);
					continue;
				}
				currentToken.type = TokenType.arg;
				tokensOut.Add(currentToken);
				break;
		}

		lastCheckedToken = i;
	}

	return tokensOut;
}

string tokens_to_asm(IList<Token> tokens, string code)
{
	string output = "";
	List<TokenType> expectType = [];
	string currentFuncCall = "";
	Stack currentSectionStack = new();
	currentSectionStack.Push(SectionType.none);
	string args = "";
	for (int i = 0; i < tokens.Count(); i++)
	{
		Token token = tokens[i];
		if (expectType.Count > 0)
		{
			if (expectType[0] != token.type)
			{
				throw new Exception($"Error at {token.line}:{token.start}: expected '{Consts.TokenTypeStrings[(int)expectType[0]]}' but got '{token.value}'");
			}
			expectType.RemoveAt(0);
		}
		if (currentSectionStack.Count > 0 && (SectionType)currentSectionStack.Peek() == SectionType.vars)
		{
			switch (token.type)
			{
				case TokenType.sizedType:
					expectType.Add(TokenType.int_lit);
					expectType.Add(TokenType.none);
					expectType.Add(TokenType.semi);
					// args = "dword ";
					args = $"{Consts.SizedTypesBss[Array.IndexOf(Consts.SizedTypes, token.type)]} ";
					break;
				case TokenType.int_lit:
					// args = "dword 1";
					args += $"{token.type}";
					break;
				case TokenType.none:
					// output += "stdOutHandle resd 1
					output += $"{token.value} {args}\n";
					args = "";
					Vars.Add(new Var());
					break;
				case TokenType.semi:
					break;
				default:
					throw new Exception($"Error at {token.line}:{token.start}: Expected a sized type (byte, word, dword, qword, etc.), but got '{token.value}'");
			}
			continue;
		}
		switch (token.type)
		{
			case TokenType._extern:
				if (currentSectionStack.Count != 1)
				{
					throw new Exception($"Error at {token.line}:{token.start}: externs must be defined outside of any section");
				}
				expectType.Add(TokenType.semi);
				output += $"extern {token.value}\n";
				break;
			case TokenType._return:
				if ((SectionType)currentSectionStack.Peek() != SectionType.prgm)
				{
					throw new Exception($"Error at {token.line}:{token.start}: Invalid keyword in current section \"{currentSectionStack.Peek()}\"");
				}
				if (i + 2 < tokens.Count() && tokens[i + 2].type == TokenType.semi)
				{
					output += $"push {tokens[i + 1].value}\nret\n";
				}
				else if (tokens[i + 1].type == TokenType.semi)
				{
					output += "ret\n";
				}
				else
				{
					throw new Exception(
						$"Error: missing semicolon on line {tokens[i + 2].line}\n" +
						$"return {tokens[i + 1].value}");
				}
				i += 2;
				break;
			case TokenType.section:
				if (tokens[i + 1].value != "{")
				{
					throw new Exception(
						$"Error at {token.line}:{token.start}: Expected '{{' but got '{tokens[i + 1].value}'\n" +
						$"section {tokens[i + 1].value}\n" +
						$"        {String.Concat(Enumerable.Repeat(" ", tokens[i + 1].value.Length))}^");
				}
				if ((SectionType)currentSectionStack.Peek() != SectionType.none)
				{
					throw new Exception($"Error at {token.line}:{token.start}: Expected '}}' but got section");
				}
				switch (token.value)
				{
					case "data":
						output += "section .data\n";
						currentSectionStack.Push(SectionType.data);
						break;
					case "vars":
						output += "section .bss\n";
						currentSectionStack.Push(SectionType.vars);
						break;
					case "prgm":
						output += "section .text\n";
						currentSectionStack.Push(SectionType.prgm);
						break;
				}
				i+= 1;
				break;
			case TokenType.int_lit:
				throw new Exception(
					$"Error, random integer literal at {token.line}:{token.start}\n" +
					$"{token.value}\n" +
					String.Concat(Enumerable.Repeat("^", token.value.Length)));
			case TokenType.funcCall:
				currentFuncCall = token.value;
				break;
			case TokenType.global:
				if (tokens[i + 1].type != TokenType.label)
				{
					throw new Exception($"Error at {tokens[i + 1].line}:{tokens[i + 1].start}: Expected keyword 'label' but got {tokens[i + 1].value}");
				}
				output += $"global {tokens[i + 1].value}\n";
				expectType.Add(TokenType.label);
				break;
			case TokenType.label:
				currentSectionStack.Push(SectionType.label);
				output += $"{token.value}:\n";
				expectType.Add(TokenType.ignoreChar);
				break;
			case TokenType.argsStart:
				break;
			case TokenType.argsEnd:
				expectType.Add(TokenType.semi);
				string joinedStr = String.Join("", args.Reverse());
				output += $"{joinedStr}call {currentFuncCall}\n";
				args = "";
				currentFuncCall = "";
				break;
			case TokenType.arg:
				// Reverse returns an IEnumerable<char>, have to use String.Join with a blank string to convert it back to a string,
				// using ToString() returns type name
				args += String.Join("", $"push {token.value}\n".Reverse());
				break;
			case TokenType.semi:
				//output += "\r\n";
				break;
			case TokenType.keyword:
				//Output += token.value;
				throw new Exception($"Error at {token.line}:{token.start}: Unhandled keyword \"{token.value}\"");
			case TokenType.ignoreChar:
				if (token.value == "}")
				{
					if (currentSectionStack.Count <= 1)//(SectionType)currentSectionStack.Peek() != SectionType.none)
					{
						throw new Exception($"Error at {token.line}:{token.start}: Unexpected symmbol '}}'");
					}
					currentSectionStack.Pop();
				}
				break;
			case TokenType._operator:
				switch (token.value)
				{
					case "+=":
						output += $"add {tokens[i + 1].value}, {tokens[i + 2].value}\n";
						break;
					case "-=":
						output += $"sub {tokens[i + 1].value}, {tokens[i + 2].value}\n";
						break;
					case "/=":
						if (!IsRegister(tokens[i + 1].value))
						{
							throw new Exception($"Error at {token.line}:{token.start}: Cannot divide a value in memory");
						}
						else if (!IsRegister(tokens[i + 2].value))
						{
							throw new Exception($"Error at {token.line}:{token.start}: Cannot divide by a value in memory");
						}
						// TODO: Improve if possible
						char? registerPrefix = '\0';
						string pushString = ""; // Contains all push statements at beginning of operation to store registers
												// and mov statements to move the correct values into the correct registers
						string movString = ""; // Contains string after div statement 
						string popString = ""; // Contains all pop statements to reset the pushed and moved registers
						string divBy = tokens[i + 2].value; // Register to devide by

						if ((selectedCommandLineOptions & (uint)CommandLineOptions.x64) == (uint)CommandLineOptions.x64)
						{
							// Compile to 64-bit
							registerPrefix = 'r';
						}
						else
						{
							// Compile to 32-bit
							registerPrefix = 'e';
						}

						// tokens[i + 1] operator tokens[i + 2]
						// eg.  eax         /=         ecx
						if (tokens[i + 1].value != $"{registerPrefix}ax")
						{
							pushString +=
								$"push {registerPrefix}ax\n" +
								$"mov {registerPrefix}ax, {tokens[i + 1].value}\n";
							movString += $"mov {tokens[i + 1]}, {registerPrefix}ax\n";
							popString += $"pop {registerPrefix}ax\n";
						}

						if (tokens[i + 2].type == TokenType.int_lit)
						{
							pushString +=
								$"push {registerPrefix}bx\n" +
								$"mov {registerPrefix}bx, {tokens[i + 2].value}\n";
							popString = $"pop {registerPrefix}bx\n" + popString;

							divBy = $"{registerPrefix}bx";
						}

						output += $"{pushString}div {divBy}\n{movString}{popString}";
						break;
					case "*=":
						if (!IsRegister(tokens[i + 1].value))
						{
							throw new Exception($"Error at {token.line}:{token.start}: Cannot multiply a value in memory");
						}
						else if (!IsRegister(tokens[i + 2].value))
						{
							throw new Exception($"Error at {token.line}:{token.start}: Cannot multiply by a value in memory");
						}
						// TODO: Improve if possible
						registerPrefix = '\0';
						pushString = ""; // Contains all push statements at beginning of operation to store registers
										 // and mov statements to move the correct values into the correct registers
						movString = ""; // Contains string after div statement 
						popString = ""; // Contains all pop statements to reset the pushed and moved registers
						string mulBy = tokens[i + 2].value; // Register to devide by
						Registers.RegisterSizes inputSize;     // User will give register name in size of current program,
						Registers.RegisterSizes necessarySize; // but multiplication can only happen with a register of half
															   // the size of the current program size.
															   // ex. an x86 assembly program can only do multiplication
															   // bewtween 2 16 bit registers
															   // but multiplication can only happen with a register of half

						if ((selectedCommandLineOptions & (uint)CommandLineOptions.x64) == (uint)CommandLineOptions.x64)
						{
							// Compile to 64-bit
							registerPrefix = 'e';
							inputSize = Registers.RegisterSizes._64;
							necessarySize = Registers.RegisterSizes._32;
						}
						else
						{
							// Compile to 32-bit
							registerPrefix = null;
							inputSize = Registers.RegisterSizes._32;
							necessarySize = Registers.RegisterSizes._16;
						}

						// tokens[i + 1] token tokens[i + 2]
						// eg.  eax       *=        ecx
						if (Registers.RegisterConvert(tokens[i + 1], inputSize, necessarySize) != $"{registerPrefix}ax")
						{
							pushString +=
								$"push {registerPrefix}ax\n" +
								$"mov {registerPrefix}ax, {tokens[i + 1].value}\n";
							movString += $"mov {tokens[i + 1].value}, {registerPrefix}ax\n";
							popString += $"pop {registerPrefix}ax\n";
						}

						if (tokens[i + 2].type == TokenType.int_lit)
						{
							mulBy = $"{registerPrefix}bx";
							pushString +=
								$"push {registerPrefix}bx\n" +
								$"mov {registerPrefix}bx, {tokens[i + 2].value}\n";
							popString = $"pop {registerPrefix}bx\n" + popString;
						}

						output += $"{pushString}mul {mulBy}\n{movString}{popString}";
						break;
					case "=":
						bool op0IsVar = !IsRegister(tokens[i + 1].value);
						bool op1IsVar = !IsRegister(tokens[i + 2].value);
						// if both operands are in memory
						if (!(op0IsVar ^ op1IsVar))
						{

						}
						output += $"mov {tokens[i + 1].value}, {tokens[i + 2].value}\n";
						break;
					case "&=":
						// TODO: Implement operations
					case "|=":
					case "^=":
						break;
					default:
						break;
				}
				i += 2;
				break;
			default:
				if (IsRegister(token.value))
				{
					break;
				}
				throw new Exception($"Error at {token.line}:{token.start}: Unkown keyword \"{token.value}\"");
		}
	}
	if (expectType.Count > 0)
	{
		throw new Exception($"Error: Expected '{Consts.TokenTypeStrings[(int)expectType[0]]}' but got end of file");
	}
	return output;
}

try
{
	List<Token> tokens = SplitString(FileString);
	Tokens = Tokenize(tokens);
	Output = tokens_to_asm(Tokens, FileString);
	string compiledPath = args[0][..^2];
	if (File.Exists(compiledPath))
	{
		File.Delete(compiledPath);
	}
	File.Create(compiledPath).Close();
	File.WriteAllText(compiledPath, Output);
}
catch (Exception e)
{
	Output = $"{e.Message}";
	if ((selectedCommandLineOptions & (uint)CommandLineOptions.showStacktrace) == 0b_0001)
	{
		Output += $"\nStacktrace:\n{e.StackTrace}";
	}
}
try
{
	Console.Write($"{Output}\n");
}
catch(Exception e)
{
	Console.WriteLine(e.Message);
}
return;

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
	showStacktrace		= 0b_0001,
	entryPointDefined	= 0b_0010,
	x64					= 0b_0100
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
	_operator,  // TODO: Implement &=, |=, ^=, ++, --, <<, >>
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
	/// a location in memory, but not the memory at the location
	/// </summary>
	memoryLocation,
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
	public int sizeBytes;
	/// <summary>
	/// defined size in unit is was defined with
	/// </summary>
	public int size;
	/// <summary>
	/// unit used to define var size
	/// </summary>
	public string unit;
	public Var(string name, int sizeBytes, int size, string unit)
	{
		this.name = name;
		this.sizeBytes = sizeBytes;
		this.size = size;
		this.unit = unit;
	}
}
public struct Token
{
	public TokenType type;
	public string value;
	public int start;
	public int end;
	public int line;

	public Token(TokenType type = TokenType.none, string value = "", int start = 0, int end = 0, int line = 0)
	{
		this.type = type;
		this.value = value;
		this.start = start;
		this.end = end;
		this.line = line;
	}
}


public static class Consts
{
	public static readonly string[] TokenTypeStrings = { "\0", "extern", "ret", "section", "intager litteral", "call", ":", "global", "\"", "=", "(", ")", ",", ";", "keyword", "", "=, +=, -=, *=, etc.", "al, ah, bl, ah, etc.", "ax, bx, cx, dx, etc.", "eax, ebx, ecx, edx, etc.", "rax, rbx, rcx, rdx, etc." };
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
