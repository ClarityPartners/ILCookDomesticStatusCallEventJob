﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------
namespace ILCookDomesticStatusCalEventJob.Entities
{
    using System.Xml.Serialization;

    // 
    // This source code was auto-generated by xsd, Version=4.7.2053.0.
    // 


    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.7.2053.0")]
    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    [System.Xml.Serialization.XmlRootAttribute(Namespace = "", ElementName = "Result", IsNullable = false)]
    public partial class AddHearingResultEntity
    {

        private string hearingIDField;

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(DataType = "integer")]
        public string HearingID
        {
            get
            {
                return this.hearingIDField;
            }
            set
            {
                this.hearingIDField = value;
            }
        }
    }
}