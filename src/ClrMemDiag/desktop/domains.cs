using System;
using System.Collections.Generic;
using Address = System.UInt64;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    class DesktopAppDomain : ClrAppDomain
    {
        /// <summary>
        /// Address of the AppDomain.
        /// </summary>
        public override Address Address { get { return m_address; } }

        /// <summary>
        /// The AppDomain's ID.
        /// </summary>
        public override int Id { get { return m_id; } }

        /// <summary>
        /// The name of the AppDomain, as specified when the domain was created.
        /// </summary>
        public override string Name { get { return m_name; } }
        public override IList<ClrModule> Modules { get { return m_modules; } }

        internal int InternalId { get { return m_internalId; } }

        public override string ConfigurationFile
        {
            get { return m_runtime.GetConfigFile(m_address); }
        }

        public override string AppBase
        {
            get
            {
                
                string appBase = m_runtime.GetAppBase(m_address);
                if (string.IsNullOrEmpty(appBase))
                    return null;

                Uri uri = new Uri(appBase);
                try
                {
                    return uri.AbsolutePath.Replace('/', '\\');
                }
                catch (InvalidOperationException)
                {
                    return appBase;
                }
            }
        }

        internal DesktopAppDomain(DesktopRuntimeBase runtime, IAppDomainData data, string name)
        {
            m_address = data.Address;
            m_id = data.Id;
            m_name = name;
            m_internalId = s_internalId++;
            m_runtime = runtime;
        }

        internal void AddModule(ClrModule module)
        {
            m_modules.Add(module);
        }

        #region Private
        private Address m_address;
        private string m_name;
        private int m_id, m_internalId;
        private List<ClrModule> m_modules = new List<ClrModule>();
        private DesktopRuntimeBase m_runtime;

        static int s_internalId;
        #endregion
    }
}
