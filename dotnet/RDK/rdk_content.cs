using System;
using System.Collections.Generic;

#if USING_RDK

namespace Rhino.Render
{
  [Flags]
  public enum RenderContentStyle : int
  {
    None = 0,
    /// <summary>
    /// Texture UI includes an auto texture summary section. See AddAutoParameters().
    /// </summary>
    TextureSummary = 0x0001,
    /// <summary>
    /// Editor displays an instant preview before preview cycle begins.
    /// </summary>
    QuickPreview = 0x0002,
    /// <summary>
    /// Content's preview imagery can be stored in the preview cache.
    /// </summary>
    PreviewCache = 0x0004,
    /// <summary>
    /// Content's preview imagery can be rendered progressively.
    /// </summary>
    ProgressivePreview = 0x0008,
    /// <summary>
    /// Texture UI includes an auto local mapping section for textures. See AddAutoParameters()
    /// </summary>
    LocalTextureMapping = 0x0010,
    /// <summary>
    /// Texture UI includes a graph section.
    /// </summary>
    GraphDisplay = 0x0020,
    /// <summary>
    /// Content supports UI sharing between contents of the same type id.
    /// </summary>
    SharedUI = 0x0040,
    /// <summary>
    /// Texture UI includes an adjustment section.
    /// </summary>
    Adjustment = 0x0080,
    /// <summary>
    /// Content uses fields to facilitate data storage and undo support. See Fields()
    /// </summary>
    Fields = 0x0100,
    /// <summary>
    /// Content supports editing in a modal editor.
    /// </summary>
    ModalEditing = 0x0200,
  }

  [AttributeUsage(AttributeTargets.Class)]
  public sealed class CustomRenderContentAttribute : System.Attribute
  {
    private RenderContentStyle m_style = RenderContentStyle.None;
    private bool m_bIsImageBased; // = false; initialized by runtime
    private readonly Guid m_renderengine_id;

    public CustomRenderContentAttribute()
    {
      m_renderengine_id = Guid.Empty;
    }
    public CustomRenderContentAttribute(string renderEngineGuid )
    {
      m_renderengine_id = new Guid(renderEngineGuid);
    }

    public Guid RenderEngineId
    {
      get { return m_renderengine_id; }
    }

    public RenderContentStyle Styles
    {
      get { return m_style; }
      set { m_style = value; }
    }

    public bool ImageBased
    {
      get { return m_bIsImageBased; }
      set { m_bIsImageBased = value; }
    }
  }

  public abstract class RenderContent : IDisposable
  {
    /// <summary>
    /// Render content is automatically registered for the Assembly that a plug-in is defined in. If
    /// you have content defined in a different assembly (for example a Grasshopper component), then
    /// you need to explicitly call RegisterContent.
    /// </summary>
    /// <param name="assembly">Assembly where custom content is defined</param>
    /// <param name="pluginId">Parent plug-in for this assembly</param>
    /// <returns>array of render content types registered on succes. null on error</returns>
    public static Type[] RegisterContent(System.Reflection.Assembly assembly, System.Guid pluginId)
    {
      Rhino.PlugIns.PlugIn plugin = Rhino.PlugIns.PlugIn.GetLoadedPlugIn(pluginId);
      if (plugin == null)
        return null;
      Type[] exported_types = assembly.GetExportedTypes();
      if (exported_types != null)
      {
        List<Type> content_types = new List<Type>();
        for (int i = 0; i < exported_types.Length; i++)
        {
          Type t = exported_types[i];
          if (!t.IsAbstract && t.IsSubclassOf(typeof(Rhino.Render.RenderContent)) && t.GetConstructor(new Type[] { }) != null)
            content_types.Add(t);
        }

        if (content_types.Count == 0)
          return null;

        // make sure that content types have not already been registered
        for( int i=0; i<content_types.Count; i++ )
        {
          if (RdkPlugIn.RenderContentTypeIsRegistered(content_types[i]))
            return null; //just bail
        }

        RdkPlugIn rdk_plugin = RdkPlugIn.GetRdkPlugIn(plugin);
        if (rdk_plugin == null)
          return null;

        rdk_plugin.AddRegisteredContentTypes(content_types);
        int count = content_types.Count;
        Guid[] ids = new Guid[count];
        for (int i = 0; i < count; i++)
        {
          ids[i] = content_types[i].GUID;
          if (content_types[i].IsSubclassOf(typeof(RenderTexture)))
            UnsafeNativeMethods.Rdk_AddTextureFactory(ids[i]);
        }
        return content_types.ToArray();
      }
      return null;
    }

    static RenderContent FromPointer(IntPtr pRenderContent)
    {
      if (pRenderContent == IntPtr.Zero) return null;
      int serial_number = UnsafeNativeMethods.CRhCmnRenderContent_IsRhCmnDefined(pRenderContent);
      if (serial_number > 0)
        return FromSerialNumber(serial_number);

      IntPtr pTexture = UnsafeNativeMethods.Rdk_RenderContent_DynamicCastToTexture(pRenderContent);
      if (pTexture != IntPtr.Zero)
        return new NativeRenderTexture(pTexture);
      // Eventually we should never have to return a NativeRenderContent class
      return new NativeRenderContent(pRenderContent);
    }

    // you never derive directly from RenderContent
    internal RenderContent()
    {
      // This constructor is being called because we have a custom .NET subclass
      m_runtime_serial_number = m_serial_number_counter++;
      m_all_custom_content.Add(this);
    }

    internal RenderContent(IntPtr pRenderContent)
    {
      // Could be from anywhere
      m_pRenderContent = pRenderContent;
      // serial number stays zero and this is not added to the custom content list
    }


    public abstract String Name { get; }
    public abstract String Description { get; }

    public virtual void AddUISections()
    {
      IntPtr pThis = NonConstPointer();
      UnsafeNativeMethods.Rdk_CallAddUISectionsBase(pThis);
    }

    public bool AddAutomaticUISection(string caption, int id)
    {
      return UnsafeNativeMethods.Rdk_CoreContent_AddAutomaticUISection(NonConstPointer(), caption, id);
    }

    // ?? What does this mean ??
    public string ChildSlotNameFromParamName(String paramName)
    {
      using (Rhino.Runtime.StringHolder sh = new Rhino.Runtime.StringHolder())
      {
        IntPtr pString = sh.NonConstPointer();
        IntPtr pConstThis = ConstPointer();
        UnsafeNativeMethods.Rdk_RenderContent_ChildSlotNameFromParamName(pConstThis, paramName, pString);
        return sh.ToString();
      }
    }

    // ?? What does this mean ??
    public string ParamNameFromChildSlotName(String childSlotName)
    {
      using (Rhino.Runtime.StringHolder sh = new Rhino.Runtime.StringHolder())
      {
        IntPtr pString = sh.NonConstPointer();
        IntPtr pConstThis = ConstPointer();
        UnsafeNativeMethods.Rdk_RenderContent_ParamNameFromChildSlotName(pConstThis, childSlotName, pString);
        return sh.ToString();
      }
    }

    public RenderContent FindChild(String childSlotName)
    {
      IntPtr pConstThis = ConstPointer();
      // ?? Who is in charge of deleting pChild - As it stands pChild will be deleted by the new
      // ?? NativeRenderContent class if one is created
      IntPtr pChild = UnsafeNativeMethods.Rdk_RenderContent_FindChild(pConstThis, childSlotName);
      return RenderContent.FromPointer(pChild);
    }

    //virtual bool IsContentTypeAcceptableAsChild(const UUID& uuidType, const wchar_t* wszChildSlotName) const;
    //virtual bool IsFactoryProductAcceptableAsChild(const IRhRdkContentFactory* pFactory, const wchar_t* wszChildSlotName) const;

    #region C++->C# Callbacks
    internal delegate void GetAddUISectionsCallback(int serial_number);
    internal static GetAddUISectionsCallback m_AddUISections = OnAddUISections;
    static void OnAddUISections(int serial_number)
    {
      try
      {
        RenderTexture texture = RenderContent.FromSerialNumber(serial_number) as RenderTexture;
        if (texture != null)
          texture.AddUISections();
      }
      catch (Exception ex)
      {
        Rhino.Runtime.HostUtils.ExceptionReport(ex);
      }
    }

    internal delegate void RenderContentDeleteThisCallback(int serial_number);
    internal static RenderContentDeleteThisCallback m_DeleteThis = OnDeleteThis;
    static void OnDeleteThis(int serial_number)
    {
      try
      {
        RenderContent content = RenderContent.FromSerialNumber(serial_number);
        if (content != null)
        {
          content.m_pRenderContent = IntPtr.Zero;
        }
      }
      catch (Exception ex)
      {
        Rhino.Runtime.HostUtils.ExceptionReport(ex);
      }
    }


    internal delegate void GetRenderContentStringCallback(int serial_number, bool bName, IntPtr pON_wString);
    internal static GetRenderContentStringCallback m_GetRenderContentString = OnGetRenderContentString;
    static void OnGetRenderContentString( int serial_number, bool bName, IntPtr pON_wString )
    {
      try
      {
        RenderContent content = RenderContent.FromSerialNumber(serial_number);
        if (content != null)
        {
          string str = bName ? content.Name : content.Description;
          if (!string.IsNullOrEmpty(str))
            UnsafeNativeMethods.ON_wString_Set(pON_wString, str);
        }
      }
      catch(Exception ex)
      {
        Rhino.Runtime.HostUtils.ExceptionReport(ex);
      }
    }
    #endregion

    #region pointer tracking
    internal IntPtr m_pRenderContent = IntPtr.Zero;
    internal readonly int m_runtime_serial_number;// = 0; initialized by runtime
    static int m_serial_number_counter = 1;
    static readonly List<RenderContent> m_all_custom_content = new List<RenderContent>();
    internal static RenderContent FromSerialNumber(int serial_number)
    {
      int index = serial_number - 1;
      if (index >= 0 && index < m_all_custom_content.Count)
      {
        RenderContent rc = m_all_custom_content[index];
        if (rc != null && rc.m_runtime_serial_number == serial_number)
          return rc;
      }
      return null;
    }

    internal IntPtr ConstPointer()
    {
      // we might want to check for IntPtr.Zero and throw
      // an ObjectDisposedException
      return m_pRenderContent;
    }
    internal IntPtr NonConstPointer()
    {
      return m_pRenderContent;
    }
    #endregion

    #region disposable implementation
    ~RenderContent()
    {
      Dispose(false);
    }

    public void Dispose()
    {
      Dispose(true);
      GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
      if (IntPtr.Zero != m_pRenderContent)
      {
        UnsafeNativeMethods.Rdk_RenderContent_DeleteThis(m_pRenderContent);
        m_pRenderContent = IntPtr.Zero;
      }
    }
    #endregion
  }

  
  // DO NOT make public
  class NativeRenderContent : RenderContent
  {
    public NativeRenderContent(IntPtr pRenderContent)
      : base(pRenderContent)
    {
    }
    public override string Name  { get { return "TODO"; } }
    public override string Description { get { return "TODO"; } }
  }
}

#endif