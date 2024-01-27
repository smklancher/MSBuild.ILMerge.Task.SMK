//-----------------------------------------------------------------------
// <copyright file="Task.cs" company="Alexander Nosenko">
//     Copyright (c) 2013 Alexander Nosenko
// </copyright>
// <author>Alexander Nosenko</author>
//-----------------------------------------------------------------------

#region The MIT License (MIT)
/*
    Copyright (c) 2013 Alexander Nosenko

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in all
    copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
    SOFTWARE.
*/
#endregion

using System.Runtime.Versioning;

namespace MSBuild.ILMerge
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Reflection;
    using Microsoft.Build.Framework;
    using Microsoft.Build.Utilities;
    using System.Dynamic;
    using System.Text;

    /// <summary>
    /// The MSBuild task wrapping (in)famous ILMerge utility. 
    /// The recommended way of embedding dependencies into the executable is now <b>Costura.Fody</b>,
    /// but <b>ILMerge is still unavoidable for some tasks, e.g. creation of database-stored MS CRM plug-ins.</b>
    /// Also, calling ILMerge from a batch file is sometimes hindered by the limitation on the total length of the command line...
    /// </summary>
    /// <remarks>
    /// See http://sedodream.com/PermaLink,guid,020fd1af-fb17-4fc9-8336-877c157eb2b4.aspx
    /// why we don't really know the project directory so you better make sure all paths are absolute.
    /// </remarks>
    public class Task : Microsoft.Build.Utilities.Task
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Task"/> class.
        /// </summary>
        public Task()
        {
            this.LibraryPath = new ITaskItem[0];
            this.InputAssemblies = new ITaskItem[0];
        }

        #region public properties

        #region Files and directories
        /// <summary>
        /// Gets or sets the item list containing the input assemblies.
        /// The first element of the list is considered to be the primary assembly.
        /// </summary>
        /// <remarks>Translates to ILMerge.SetInputAssemblies().</remarks>
        [Required]
        public virtual ITaskItem[] InputAssemblies { get; set; }

        /// <summary>
        /// Gets or sets the item list containing the library assemblies
        /// that are not to be included in the merge. They are used to determine the 
        /// <see cref="LibraryPath"/> for the lack of a better method.
        /// With NuGet we can have hundreds potential library directories even if we filter them by platform
        /// (nontrivial task by itself).
        /// </summary>
        /// <remarks>Translates to ILMerge.SetSearchDirectories() eventually with a higher priority then <see cref="LibraryPath"/>.
        /// TODO: copy to temp dir (use symlinks?), add dthis dir to the lib path.</remarks>
        public virtual ITaskItem[] LibraryAssemblies { get; set; }

        /// <summary>
        /// Gets or sets the item list containing the directories to be used to search for input assemblies.
        /// </summary>
        /// <remarks>Translates to ILMerge.SetSearchDirectories().</remarks>
        public virtual ITaskItem[] LibraryPath { get; set; }

        /// <summary>
        /// Gets or sets the directory path to be considered an anchor point
        /// for library (usually packaged) assemblies. used to determine the default merge order.
        /// </summary>
        public virtual string PackagesDir { get; set; }

        /// <summary>
        /// Gets or sets the path and name of the file containing the merge order list.
        /// Assemblies (or, in the future, packages) are listed one per line;
        /// all names given before a line containing "..." are popped to the top of the merge order,
        /// all names after that line are pushed to the bottom. Assemblies not mentioned there stay where they were
        /// (most often the initial order was quite random).
        /// </summary>
        public virtual string MergeOrderFile { get; set; }

        /// <summary>
        /// Gets or sets the path and name of the output file with the successfully merged result assembly.
        /// </summary>
        [Required]
        public virtual string OutputFile { get; set; }

        /// <summary>
        /// Gets or sets the path and name of the log file.
        /// </summary>
        public virtual string LogFile { get; set; }

        /// <summary>
        /// Gets or sets the path name of the file that will be used to identify types that are not to have their visibility modified.
        /// Used together with <see cref="Internalize"/> flag. For details, see ILMerge documentation.
        /// </summary>
        public virtual string InternalizeExcludeFile { get; set; }

        /// <summary>
        /// Gets or sets the path and name of the attribute assembly, 
        /// an assembly that will be used to get all of the assembly-level attributes such as Culture, Version, etc.
        /// It will also be used to get the Win32 Resources from. It is mutually exclusive with the <see cref="CopyAttributes"/> property
        /// For details, see ILMerge documentation.
        /// </summary>
        public virtual string AttributeFile { get; set; }

        /// <summary>
        /// Gets or sets the path and name of the .snk file.
        /// The target assembly will be signed with its contents and will then have a strong name. 
        /// It can be used with the <see langword="DelaySign"/> property to have the target assembly delay signed.
        /// This can be done even if the primary assembly was fully signed.
        /// </summary>
        public virtual string KeyFile { get; set; }

        #endregion

        #region flags and options

        /// <summary>
        /// Gets or sets of the type names that are allowed to be in duplicate.
        /// For details, see ILMerge documentation.
        /// </summary>
        public virtual string AllowDuplicateType { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether, if the <see cref="CopyAttributes"/> is also set, 
        /// any assembly-level attributes names that have the same type are copied over into the target assembly
        /// as long as the definition of the attribute type specifies that <b>AllowMultiple</b> is true.
        /// </summary>
        public virtual bool AllowMultipleAssemblyLevelAttributes { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether, if an assembly's PeKind flag 
        /// (this is the value of the field listed as .corflags in the Manifest) is zero 
        /// it will be treated as if it was ILonly. 
        /// For details, see ILMerge documentation.
        /// </summary>
        public virtual bool AllowZeroPeKind { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether any wild cards in file names are expanded and all matching files will be used as input.
        /// Usually it is already done by MSBuild, but left here for completeness. For details, see ILMerge documentation.
        /// </summary>
        public bool AllowWildCards { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the "transitive closure"
        /// of the input assemblies is computed and added to the list of input assemblies.
        /// For details, see ILMerge documentation.
        /// </summary>
        public virtual bool Closed { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether  the assembly level attributes of each input assembly are copied over into the target assembly. 
        /// For details, see ILMerge documentation.
        /// </summary>
        public virtual bool CopyAttributes { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether ILMerge creates a .pdb file for the output assembly
        /// and merges into it any .pdb files found for input assemblies.
        /// </summary>
        public virtual bool DebugInfo { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the target assembly will be delay signed.
        /// Only used together with <see cref="KeyFile"/> option.
        /// </summary>
        public virtual bool DelaySign { get; set; }

        /// <summary>
        /// Gets or sets the file alignment used for the target assembly. 
        /// The setter sets the value to the largest power of two that is no larger than the supplied argument, and is at least 512. 
        /// </summary>
        public virtual int FileAlignment { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether types in assemblies other than the primary assembly have their visibility modified. 
        /// For details, see ILMerge documentation.
        /// </summary>
        public virtual bool Internalize { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether XML documentation files are merged 
        /// to produce an XML documentation file for the target assembly.
        /// </summary>
        public virtual bool XmlDocumentation { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether external assembly references in the manifest of the target assembly 
        /// will use full public keys (false) or public key tokens (true). Default is <see langword="true"/>.
        /// </summary>
        public virtual bool PublicKeyTokens { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether log messages are written. It is used in conjunction with the <see cref="LogFile"/> property. 
        /// </summary>
        public virtual bool ShouldLog { get; set; }

        /// <summary>
        /// Gets the value indicating whether after the merge the primary assembly had a strong name, 
        /// but the target assembly does not. This can occur when an .snk file is not specified,
        /// or if something goes wrong trying to read its contents.
        /// </summary>
        [Output]
        public virtual bool StrongNameLost { get; private set; }


        /// <summary>
        /// Gets or sets the kind of the target assembly (a library, a console application or a Windows application).
        /// The possible values are (Dll, Exe, WinExe).
        /// </summary>
        public virtual string TargetKind { get; set; }

        /// <summary>
        /// Gets or sets the version of the target framework. Default is "40".
        /// </summary>
        public virtual string TargetPlatform { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether types with the same name are all merged into a single type in the target assembly.
        /// For details, see ILMerge documentation.
        /// </summary>
        public virtual bool UnionMerge { get; set; }

        /// <summary>
        /// Gets or sets the version number of the assembly in "6.2.1.3" format. Default is "1.0.0.0".
        /// </summary>
        public virtual string Version { get; set; }

        #endregion

        #endregion

        #region public methods

        /// <summary>
        /// The one and only.
        /// </summary>
        /// <returns>Success or failure.</returns>
        /// <remarks>use Dynamic. Segregate executable search in a separate class.</remarks>
        public override bool Execute()
        {
            // handle nuget packages with reference assemblies
            var input=ReplaceRefNugetAssemblies();

            var targetPlatform = ConvertTargetPlatform(this.TargetPlatform);
            var targetPlatformDir = this.GetTargetPlatformDirectory(this.TargetPlatform);
            var inputAssemblies = this.ReshuffleInputAssemblies(input);
            var searchDirs = this.CollectAllLibraryPaths();

            if (this.DebugInfo || this.ShouldLog)
            {
                ListItems("Running ILMerge executable",
                    new string[] {
                        "AllowMultipleAssemblyLevelAttributes = " + this.AllowMultipleAssemblyLevelAttributes,
                        "AllowWildCards = " + this.AllowWildCards,
                        "AllowZeroPeKind = " + this.AllowZeroPeKind,
                        "AttributeFile = " + this.BuildPath(this.AttributeFile),
                        "Closed = " + this.Closed,
                        "CopyAttributes = " + this.CopyAttributes,
                        "DebugInfo = " + this.DebugInfo,
                        "DelaySign = " + this.DelaySign,
                        "ExcludeFile = " + this.BuildPath(this.InternalizeExcludeFile),
                        "FileAlignment = " + (this.FileAlignment > 0 ? this.FileAlignment : 512),
                        "Internalize = " + this.Internalize,
                        "KeyFile = " + this.BuildPath(this.KeyFile),
                        "Log = " + this.ShouldLog,
                        "LogFile = " + this.BuildPath(this.LogFile),
                        "OutputFile = " + this.BuildPath(this.OutputFile),
                        "PublicKeyTokens = " + this.PublicKeyTokens,
                        "TargetKind = " + ConvertTargetKind(this.TargetKind),
                        "UnionMerge = " + this.UnionMerge,
                        "Version = " + this.Version,
                        "XmlDocumentation = " + this.XmlDocumentation,
                        "AllowDuplicateType = "+ this.AllowDuplicateType,
                        "TargetPlatform = " + targetPlatform + ";" + targetPlatformDir,
                        "InputAssemblies = " + string.Join("; ", inputAssemblies),
                        "SearchDirectories = " + string.Join("; ", searchDirs)
                     });
            }

            Assembly ilmergeExe = this.LoadILMerge();
            Type ilmergeType = ilmergeExe.GetType("ILMerging.ILMerge", true, true);
            if (ilmergeType == null)
                throw new InvalidOperationException("Cannot find 'ILMerging.ILMerge' in executable.");

            dynamic merger = Activator.CreateInstance(ilmergeType);


            merger.AllowMultipleAssemblyLevelAttributes = this.AllowMultipleAssemblyLevelAttributes;
            merger.AllowWildCards = this.AllowWildCards;
            merger.AllowZeroPeKind = this.AllowZeroPeKind;
            merger.AttributeFile = this.BuildPath(this.AttributeFile);
            merger.Closed = this.Closed;
            merger.CopyAttributes = this.CopyAttributes;
            merger.DebugInfo = this.DebugInfo;
            merger.DelaySign = this.DelaySign;
            merger.FileAlignment = this.FileAlignment > 0 ? this.FileAlignment : 512;
            merger.Internalize = this.Internalize;
            if (this.Internalize)
                merger.ExcludeFile = this.BuildPath(this.InternalizeExcludeFile);
            if (!string.IsNullOrEmpty(this.KeyFile))
                merger.KeyFile = this.BuildPath(this.KeyFile);
            merger.Log = this.ShouldLog;
            merger.LogFile = this.BuildPath(this.LogFile);
            merger.OutputFile = this.BuildPath(this.OutputFile);
            merger.PublicKeyTokens = this.PublicKeyTokens;
            merger.TargetKind = (dynamic)Enum.Parse(merger.TargetKind.GetType(), ConvertTargetKind(this.TargetKind).ToString());
            merger.UnionMerge = this.UnionMerge;
            merger.XmlDocumentation = this.XmlDocumentation;

            if (!string.IsNullOrEmpty(this.Version))
            {
                merger.Version = new Version(this.Version);

            }

            if (!string.IsNullOrEmpty(this.AllowDuplicateType))
            {
                if (this.AllowDuplicateType == "*")
                {
                    merger.AllowDuplicateType(null);
                }
                else
                {
                    foreach (string typeName in this.AllowDuplicateType.Split(','))
                    {
                        merger.AllowDuplicateType(typeName);
                    }
                }
            }

            merger.SetTargetPlatform(targetPlatform, targetPlatformDir);
            merger.SetInputAssemblies(inputAssemblies);
            merger.SetSearchDirectories(searchDirs);

            try
            {
                Log.LogMessage(
                    MessageImportance.Normal,
                    "Merging {0} assembl{1} to '{2}'.",
                    this.InputAssemblies.Length,
                    (this.InputAssemblies.Length != 1) ? "ies" : "y",
                    this.BuildPath(this.OutputFile));

                merger.Merge();
                this.StrongNameLost = merger.StrongNameLost;
                if (this.StrongNameLost)
                    Log.LogMessage(MessageImportance.High, "StrongNameLost = true");
            }
            catch (Exception exception)
            {
                Log.LogErrorFromException(exception);

                if (exception.Message.Contains("metadata"))
                    Log.LogError("Possible reason: some of the merge assemblies have portable PDBs. Try to set ILMergeDebugInfo to false.");

                return false;
            }

            return true;
        }
        #endregion

        #region private methods

        /// <summary>
        /// This is certainly not elegant but fixes the specific issues I have:
        /// * System.Buffers nuget package has a ref assembly using net45, but lib only has net461 (seems odd that is allowed).
        /// * lib.harmony has a netstandard2.0 ref assembly
        /// At this point there is no reason not to build for 4.8, so any "net4" library will work.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private string PossibleOtherTfmImplmentation(string path)
        {
            var libFolder = Path.Combine(path.Substring(0, path.IndexOf(@"\ref\")), @"lib\");
            var refTfm = Path.GetFileName(Path.GetDirectoryName(path));

            var usableTfm=string.Empty;

            // if the ref assembly as a .NET framework or standard tfm...
            if (Directory.Exists(libFolder) && (refTfm.Contains("net4") || refTfm.Contains("netstandard")))
            {
                // ordered so we end up with the highest available version
                IEnumerable<string> dirs = Directory.EnumerateDirectories(libFolder).OrderBy(x=>x);
                foreach (var dir in dirs)
                {
                    // then take any lib assembly that is .NET framework
                    if (dir.Contains("net4"))
                    {
                        usableTfm=Path.Combine(dir, Path.GetFileName(path));
                    }
                }

            }

            if (!string.IsNullOrEmpty(usableTfm))
            {
                Log.LogMessage(MessageImportance.High, $"Possible ref assembly with no matching implementation assembly...");
                Log.LogMessage(MessageImportance.High, path);
                Log.LogMessage(MessageImportance.High, $"...but guessing at possibly compatible lib assembly with different TFM:");
                Log.LogMessage(MessageImportance.High, usableTfm);
            }
            else
            {
                // don't want to throw an error since just assuming ref by path
                Log.LogWarning($"Possible ref assembly with no matching implementation assembly: {path}");

            }

            return usableTfm;
        }

        /// <summary>
        /// Nuget packages can contain reference assemblies (ref folder) and these will be used for compilation.
        /// These have no actual method bodies, so need to swap to the implmentation assembly (lib folder) before merging.
        /// This assumes that any path with a "\ref\" folder is a reference assembly in a nuget folder structure, 
        /// though only replaces if an equivolent lib file exists.
        /// </summary>
        private List<string> ReplaceRefNugetAssemblies()
        {
            var results=new List<string>();
            foreach(var item in this.InputAssemblies)
            {
                var itemToAdd = item.ItemSpec;

                // it would be more definitive to load the dll in reflection context
                // and check for the assembly level attribute ReferenceAssembly
                // but probably not worth extra peformance cost
                if (item.ItemSpec.Contains(@"\ref\"))
                {
                    var lib = item.ItemSpec.Replace(@"\ref\", @"\lib\");
                    if (File.Exists(lib))
                    {
                        // if there is a dll in a parallel "lib" folder, then use that over the "ref" folder
                        itemToAdd = lib;
                        Log.LogMessage(MessageImportance.High, $"Reference assembly replaced with implementation assembly: {itemToAdd}");
                    }
                    else
                    {
                        var otherTfmPath = PossibleOtherTfmImplmentation(item.ItemSpec);
                        if(!string.IsNullOrEmpty(otherTfmPath))
                        {
                            itemToAdd = otherTfmPath;
                        }
                    }
                }

                results.Add(itemToAdd);
            }

            return results;
        }

        private void ListItems(string message, IEnumerable<string> items)
        {
            Log.LogMessage(MessageImportance.Low, message);
            foreach (var item in items)
                Log.LogMessage(MessageImportance.Low, "   " + item);
        }

        /// <summary>
        /// Reshuffles the input assembly list according to the file source and specified lead order (if any).
        /// In any case, project assemblies are loaded before library assemblies (library assemblies are all that lives
        /// under <see cref="PackagesDir"/>).
        /// </summary>
        /// <returns>The reordered list of input assemblies. The master assembly will remain the first one.</returns>
        /// <remarks>TODO: use http://stackoverflow.com/questions/6653715/view-nuget-package-dependency-hierarchy 
        /// to flatten package dependency graph.</remarks>
        private string[] ReshuffleInputAssemblies(List<string> input)
        {
            var result = new List<string>();
            var projectFiles = new List<string>();
            var libraryFiles = new List<string>();

            result.Add(this.BuildPath(input[0]));

            for (var i = 1; i < input.Count; i++)
            {
                var fileName = this.BuildPath(input[i]);
                if (!string.IsNullOrWhiteSpace(this.PackagesDir))
                {
                    var pathInPackages = GetRelativePath(this.PackagesDir, fileName);
                    if (!Path.IsPathRooted(pathInPackages))
                    {
                        libraryFiles.Add(fileName);
                        continue;
                    }
                }

                projectFiles.Add(fileName);
            }

            if (this.DebugInfo)
            {
                ListItems("Project assemblies to merge (original order)", projectFiles);
                ListItems("Library assemblies to merge (original order)", libraryFiles);
            }

            var mergeOrderHigh = new List<string>();
            var mergeOrderLow = new List<string>();

            var isHigh = true;
            foreach (var orderItem in this.ReadMergeOrder())
            {
                if (orderItem == "...")
                    isHigh = false;
                else if (isHigh)
                    mergeOrderHigh.Add(orderItem);
                else
                    mergeOrderLow.Add(orderItem);
            }

            if (this.DebugInfo && mergeOrderHigh.Count > 0)
                ListItems("Assemblies to bubble up in the merge order", mergeOrderHigh);
            if (this.DebugInfo && mergeOrderLow.Count > 0)
                ListItems("Assemblies to push down in the merge order", mergeOrderLow);

            result.AddRange(SortAssemblies(projectFiles, mergeOrderHigh, mergeOrderLow));
            result.AddRange(SortAssemblies(libraryFiles, mergeOrderHigh, mergeOrderLow));

            var filenames = result.Select(s => Path.GetFileName(s)).ToArray();
            if (this.DebugInfo && filenames.Length > 0)
                ListItems("Final assemblies merge order", filenames);

            var tempOrdrFileName = Path.ChangeExtension(result[0], ".ilmerge");
            WriteMergeOrder(tempOrdrFileName, filenames);

            return result.Where(s => !string.IsNullOrEmpty(s)).ToArray();
        }

        private string AsDirectory(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;
            try
            {
                FileAttributes attr = File.GetAttributes(path);
                return (attr & FileAttributes.Directory) == FileAttributes.Directory
                    ? path
                    : Path.GetDirectoryName(path);
            }
            catch (Exception)
            {
                // fake directories etc can have funny names
                return null;
            }
        }

        private string[] CollectAllLibraryPaths()
        {
            var result = new List<string>();
            if (this.LibraryAssemblies != null)
            {
                result.AddRange(this.LibraryAssemblies
                    .Where(iti => iti != null)
                    .Select(iti => AsDirectory(this.BuildPath(iti.ItemSpec))));
            }

            if (this.LibraryPath != null)
            {
                result.AddRange(this.LibraryPath
                    .Where(iti => iti != null)
                    .Select(iti => AsDirectory(this.BuildPath(iti.ItemSpec))));
            }

            result = result.Where(s => !string.IsNullOrEmpty(s) && !s.StartsWith("{"))
                .Distinct()
                .ToList();
            if (this.DebugInfo && result.Count > 0)
                ListItems("Library paths", result);

            return result.ToArray();
        }

        private static string GetRelativePath(string rootPath, string fullPath)
        {
            // TODO: normalize both anchor and filename
            var relPath = fullPath;
            if (!String.IsNullOrEmpty(rootPath))
            {
                if (rootPath.Equals(fullPath, StringComparison.OrdinalIgnoreCase))
                {
                    relPath = string.Empty;
                }
                else if (fullPath.StartsWith(rootPath + "\\", StringComparison.OrdinalIgnoreCase))
                {
                    relPath = fullPath.Substring(rootPath.Length + 1);
                }
            }

            return relPath;
        }

        private string[] ReadMergeOrder()
        {
            if (string.IsNullOrWhiteSpace(this.MergeOrderFile))
                return new string[0];

            if (!File.Exists((this.MergeOrderFile)))
            {
                Log.LogWarning("Specified merge order file '{0}' doesn't exist.", this.MergeOrderFile);
                return new string[0];

            }

            var items = File.ReadAllLines(this.MergeOrderFile)
                    .Select(s => Regex.Replace(s, @"\s+", ""))
                    .Where(s => !(string.IsNullOrEmpty(s) || s.StartsWith("#") || s.StartsWith("//")))
                    .ToArray();

            if (this.DebugInfo && items.Length > 0)
                ListItems("Merge order is read from '" + this.MergeOrderFile + "' as", items);

            return items;
        }

        private void WriteMergeOrder(string fileName, string[] items)
        {
            try
            {
                Log.LogMessage("Writing assembly merge order to '{0}'.", fileName);
                File.WriteAllLines(fileName, items);
            }
            catch (System.Exception ex)
            {
                Log.LogWarning("Could not write merge order file '{0}' failed: {1}", fileName, ex.Message);
            }
        }

        private List<string> SortAssemblies(List<string> files, List<string> mergeOrderHigh, List<string> mergeOrderLow)
        {
            files = SortAssembliesDown(files, mergeOrderLow);
            return SortAssembliesUp(files, mergeOrderHigh);
        }

        private List<string> SortAssembliesUp(List<string> files, IEnumerable<string> mergeOrder)
        {
            ////ListItems("in order up", mergeOrder);
            ////ListItems("before", files);

            var result = new List<string>();
            foreach (string pattern in mergeOrder)
            {
                for (var i = 0; i < files.Count; i++)
                {
                    if (IsThatFile(files[i], pattern))
                    {
                        result.Add(files[i]);
                        files.RemoveAt(i);
                    }
                }
            }

            ////ListItems("shifted", result);
            ////ListItems("leftovers", files);

            result.AddRange(files);

            ////ListItems("after", result);

            return result;
        }

        private List<string> SortAssembliesDown(List<string> files, IEnumerable<string> mergeOrder)
        {
            ////ListItems("in order down", mergeOrder);
            ////ListItems("before", files);

            var result = new List<string>();
            foreach (string pattern in mergeOrder.Reverse())
            {
                for (var i = files.Count - 1; i >= 0; i--)
                {
                    if (IsThatFile(files[i], pattern))
                    {
                        result.Insert(0, files[i]);
                        files.RemoveAt(i);
                    }
                }
            }

            ////ListItems("shifted", result);
            ////ListItems("leftovers", files);

            result.InsertRange(0, files);

            ////ListItems("after", result);

            return result;
        }

        private string NormalizeFileName(string fileName)
        {
            fileName = Path.GetFileName(fileName).ToLowerInvariant();
            string ext = Path.GetExtension(fileName);
            if (ext == ".dll" || ext == ".exe")
                fileName = Path.ChangeExtension(fileName, string.Empty);
            return fileName;
        }

        private bool IsThatFile(string fileName, string pattern)
        {
            var fileNameParts = NormalizeFileName(fileName).Split('.');
            var patternParts = NormalizeFileName(pattern).Split('.');

            for (int i = 0; i < patternParts.Length; i++)
            {
                var p = patternParts[i];
                var wild = p == "*";

                if (i >= fileNameParts.Length)
                    return (i == patternParts.Length && wild);
                var f = fileNameParts[i];
                if (f != p && !wild)
                    return false;
            }

            ////Log.LogMessage("{0} fit {1}", fileName, pattern);
            return true;
        }

        private string BuildPath(string iti)
        {
            // see http://sedodream.com/PermaLink,guid,020fd1af-fb17-4fc9-8336-877c157eb2b4.aspx
            // why we don't really know the project directory
            // so you better make sure all paths are absolute
            return iti;

            ////return string.IsNullOrEmpty(iti)
            ////    ? null
            ////    : Path.IsPathRooted(iti)
            ////        ? iti
            ////        : Path.Combine(base.BuildEngine.ProjectFileOfTaskNode, iti);
        }

        private enum ILMergeKind
        {
            Dll = 0,
            Exe = 1,
            WinExe = 2,
            SameAsPrimaryAssembly = 3,
        }

        private ILMergeKind ConvertTargetKind(string value)
        {
            if (string.IsNullOrEmpty(value))
                return ILMergeKind.SameAsPrimaryAssembly;

            if (Enum.IsDefined(typeof(ILMergeKind), value))
            {
                return (ILMergeKind)Enum.Parse(typeof(ILMergeKind), value);
            }
            else
            {
                Log.LogWarning("Unrecognized target kind '{0}' - should be [Exe|Dll|WinExe|SameAsPrimaryAssembly]; set to SameAsPrimaryAssembly", value);
                return ILMergeKind.SameAsPrimaryAssembly;
            }
        }

        private TargetDotNetFrameworkVersion GetTargetPlatform(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return TargetDotNetFrameworkVersion.VersionLatest;

            value = Regex.Replace(value, "[^.0-9]", string.Empty);
            int n;
            if (int.TryParse(value, out n))
            {
                if (n == 1 || n == 10 || n == 11)
                    return TargetDotNetFrameworkVersion.Version11;
                if (n == 2 || n == 20)
                    return TargetDotNetFrameworkVersion.Version20;
                if (n == 3 || n == 30)
                    return TargetDotNetFrameworkVersion.Version30;
                if (n == 35)
                    return TargetDotNetFrameworkVersion.Version35;
                if (n == 4 || n == 40)
                    return TargetDotNetFrameworkVersion.Version40;
                if (n == 45)
                    return TargetDotNetFrameworkVersion.Version45;
                return TargetDotNetFrameworkVersion.VersionLatest;
            }

            Log.LogWarning("Unrecognized target platform '{0}', set to v4", new object[0]);
            return TargetDotNetFrameworkVersion.VersionLatest;
        }

        private string ConvertTargetPlatform(string value)
        {
            switch (this.GetTargetPlatform(this.TargetPlatform))
            {
                case TargetDotNetFrameworkVersion.Version11: return "v1.1";
                case TargetDotNetFrameworkVersion.Version20: return "v2";
                case TargetDotNetFrameworkVersion.Version30: return "v2";
                case TargetDotNetFrameworkVersion.Version35: return "v2";
                default: return "v4";
            }
        }

        private FrameworkName GetTargetFrameworkName(TargetDotNetFrameworkVersion version)
        {
            switch (version)
            {
                case  TargetDotNetFrameworkVersion.Version11:
                    return new FrameworkName(".NET Framework, Version=1.1");
                case TargetDotNetFrameworkVersion.Version20:
                    return new FrameworkName(".NET Framework, Version=2.0");
                case TargetDotNetFrameworkVersion.Version30:
                    return new FrameworkName(".NET Framework, Version=3.0");
                case TargetDotNetFrameworkVersion.Version35:
                    return new FrameworkName(".NET Framework, Version=3.5");
                default:
                    return new FrameworkName(".NET Framework, Version=4.0");

            }
        }

        private string GetTargetPlatformDirectory(string value)
        {
            // see http://www.hurryupandwait.io/blog/what-you-should-know-about-running-ilmerge-on-net-4-5-assemblies-targeting-net-4-0
            // about why it doesn't pass the correct Platform Directory when targeting .NET 4.0 and running under VS2015 
            // or using the Roslyn compiler with .NET 4.5 (reported by ianclegg)
            ////return ToolLocationHelper.GetPathToDotNetFramework(this.GetTargetPlatform(this.TargetPlatform));

            var fn = this.GetTargetFrameworkName(this.GetTargetPlatform(this.TargetPlatform));
            Log.LogMessage("GetTargetFrameworkName=" + fn);
            var dirs = ToolLocationHelper.GetPathToReferenceAssemblies(fn);
            Log.LogMessage("GetPathToReferenceAssemblies=" + string.Join(";", dirs));
            if (dirs.Count == 0)
                return ToolLocationHelper.GetPathToDotNetFramework(this.GetTargetPlatform(this.TargetPlatform));
            return dirs[0];
        }

        #region everyone looking for ILMerge...
        private Assembly LoadILMerge()
        {
            var ilmerge = this.FindILMergeExecutable();
            if (ilmerge == null)
                throw new FileNotFoundException("Cannot find ILMerge executable.");

            Log.LogMessage("Loading ILMerge from '{0}'.", ilmerge);
            return Assembly.LoadFrom(ilmerge);
        }

        private string FindILMergeExecutable()
        {
            string iamhere = Path.GetDirectoryName(this.GetType().Assembly.Location);
            string ilmergePath = null;

            // in the same directory as the task dll and 6 times up (in case we are package)
            if (LookForILMergeUp(iamhere, out ilmergePath))
                return ilmergePath;

            // somewhere in the parent chain of the project (in case we are not but they are)
            if (LookForILMergeUp(Path.GetDirectoryName(this.OutputFile), out ilmergePath))
                return ilmergePath;

            return null;
        }

        private bool LookForILMergeUp(string path, out string ilmergePath)
        {
            ilmergePath = null;
            for (var i = 6; i >= 0 && !string.IsNullOrEmpty(Path.GetFileName(path)); i--)
            {
                if (LookForILMergeInDirectory(path, out ilmergePath))
                    return true;
                path = Path.GetDirectoryName(path);
            }

            return false;
        }

        // ugly bit of blindly grouping around...
        private bool LookForILMergeInDirectory(string pathToTry, out string ilmergePath)
        {
            if (IsILMergeThere(pathToTry, out ilmergePath))
                return true;

            // NB: we ignore pre-release part, assuming that ILMerge will never be made public in pre-release state
            // just get the latest 
            var ilmergeDir = Directory.EnumerateDirectories(pathToTry, "ilmerge.*", SearchOption.TopDirectoryOnly)
                .Where(s =>
                {
                    s = Path.GetFileName(s);
                    // ignore all packages like ILMerge.Bla.Bla.Bla.1.2.3.4
                    string[] parts = s.Split('.');
                    int n;
                    return parts.Length == 1 || int.TryParse(parts[1], out n);
                })
                .OrderByDescending(s => s)
                .FirstOrDefault();
            if (ilmergeDir != null)
                return IsILMergeSomewhereThere(ilmergeDir, out ilmergePath);

            var packagesDir = Path.Combine(pathToTry, "packages");
            if (Directory.Exists(packagesDir))
                return LookForILMergeInDirectory(packagesDir, out ilmergePath);

            return false;
        }

        private bool IsILMergeSomewhereThere(string pathToTry, out string ilmergePath)
        {
            ilmergePath = null;

            // ILMerge executable may be in the package dir directly (2.13.0307),
            // in tools/ (2.14.1208) or in tools/net452 (3.0.29)
            // so we use whatever we find
            var dirs = new List<string>();
            dirs.Add(pathToTry);

            var versionDirs = Directory.EnumerateDirectories(pathToTry, "*", SearchOption.TopDirectoryOnly)
                .OrderByDescending(s => s)
                .ToList();
            versionDirs.Add(pathToTry);
            foreach (var versionDir in versionDirs)
            {
                var toolsDir = Path.Combine(versionDir, @"tools");
                if (Directory.Exists(toolsDir))
                {
                    dirs.Add(toolsDir);
                    dirs.AddRange(Directory.EnumerateDirectories(toolsDir, "net*", SearchOption.AllDirectories));
                }
            }
            foreach (var dir in dirs)
            {
                if (IsILMergeThere(dir, out ilmergePath))
                    return true;
            }


            return false;
        }

        private bool IsILMergeThere(string pathToTry, out string ilmergePath)
        {
            ilmergePath = null;

            ilmergePath = Path.Combine(pathToTry, @"ILMerge.exe");
            //// Log.LogMessage("looking for " + ilmergePath);
            if (File.Exists(ilmergePath))
                return true;

            return false;
        }
        #endregion

        #endregion
    }
}

