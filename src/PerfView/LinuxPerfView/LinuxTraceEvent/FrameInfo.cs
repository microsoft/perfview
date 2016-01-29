using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LinuxTracing.LinuxTraceEvent
{
	public interface Frame
	{
		string DisplayName { get; }
	}

	public struct StackFrame : Frame
	{
		public string Address { get; }
		public string Module { get; }
		public string Symbol { get; }

		public string DisplayName { get { return this.Module + "!" + this.Symbol; } }

		internal StackFrame(string address, string module, string symbol)
		{
			this.Address = address;
			this.Module = module;
			this.Symbol = symbol;
		}
	}

	public struct ProcessFrame : Frame
	{
		public string Name { get; }

		public string DisplayName { get { return this.Name; } }

		internal ProcessFrame(string name)
		{
			this.Name = name;
		}
	}

	public struct ThreadFrame : Frame
	{
		public string Name { get; }
		public int ID { get; }
		public string DisplayName { get { return string.Format("{0} ({1})", this.Name, this.ID); } }

		internal ThreadFrame(int id, string name)
		{
			this.Name = name;
			this.ID = id;
		}
	}

	public struct BlockedCPUFrame : Frame
	{
		public string Kind { get; }
		public int ID { get; }
		public string DisplayName { get { return this.Kind; } }

		internal BlockedCPUFrame(int id, string kind)
		{
			this.ID = id;
			this.Kind = kind;
		}
	}
}
