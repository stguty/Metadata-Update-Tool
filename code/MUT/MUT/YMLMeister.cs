﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MUT
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    public class YMLMeister
    {
        #region reading values
        /// <summary>
        ///     Gets the starting position of the specified tag.
        /// </summary>
        /// <param name="file"></param>
        /// <param name="tag"></param>
        /// <returns></returns>
        public int GetTagStartPos(ref string file, ref string tag)
        {
            // Get the upper bound of metadata block to 
            // prevent searches over entire content and the 
            // false hits that could generate
            int metadataEndPos = file.IndexOf("---", 4);
            return file.IndexOf(tag, 4, metadataEndPos - 4);
        }

        /// <summary>
        /// End of tag is tricky. what if it is a multi-line tag?
        /// We can't search for the next \n. We have to search for 
        /// the next tag within the metadata block. If there is none, then
        /// current tag is the last one and endPos is the last character before the "\r\n---"
        /// </summary>
        /// <param name="file"></param>
        /// <param name="startPos"></param>
        /// <returns></returns>
        public int GetTagValueEndPos(ref string file, int startPos)
        {
            // Get the upper bound of metadata block to 
            // prevent searches over entire content and the 
            // false hits that could generate
            int lineEnd = lineEnd = file.IndexOf("\n", startPos);
            if (!IsMultilineValue(ref file, startPos))
            {
                return lineEnd + 1; // include the last \n
            }

            // look for next tag, or end of metadata block.
            // we search a substring, because Match method
            // doeesn't allow us to specify a startingPos for the search.
            // the substring's zero element is really lineEnd
            // in the original string. subtract 1 to backtrack over the \n
            int ret = Regex.Match(file.Substring(lineEnd), @"([A-Za-z\._]+:)|(---)").Index;
            return ret + lineEnd - 1;
            
        }

        /// <summary>
        ///     Gets the tag itself, and its value
        /// </summary>
        /// <param name="file"></param>
        /// <param name="tag"></param>
        /// <returns></returns>
        public string GetTagAndValue(ref string file, ref string tag)
        {
            int beg = GetTagStartPos(ref file, ref tag);
            int end = GetTagValueEndPos(ref file, beg);
            return file.Substring(beg, end - beg);
        }

        /// <summary>
        /// Gets only the value of a specified tag, whether single line or multi-line
        /// </summary>
        /// <param name="file"></param>
        /// <param name="tag"></param>
        /// <returns></returns>
        public string GetValue(ref string file, ref string tag)
        {
            int beg = GetTagStartPos(ref file, ref tag);
            //find the ":" and go past it
            int begValue = file.IndexOf(':', beg) + 1;
            int end = GetTagValueEndPos(ref file, beg);
            return file.Substring(begValue, end - begValue).Trim();
        }

        /// <summary>
        ///   Gets the content up until the beginning of the line
        ///   on which the tag occurs. Default beginning is from beg of file.
        ///   Keeping the startPos param for now in case we would want to do
        ///   multiple changes in one pass. in that case the prefix would be the
        ///   range from the end of hte last line that was modified to the beginning
        ///   of the next line to be modified.
        /// </summary>
        /// <param name="file"></param>
        /// <param name="tag"></param>
        /// <param name="startPos"></param>
        /// <returns></returns>
        public string GetPrefix(ref string file, ref string tag, int startPos = 0)
        {
            int tagPos = GetTagStartPos(ref file, ref tag);
            return file.Substring(startPos, tagPos - startPos);
        }

        /// <summary>
        /// Gets the substring from the end of the tag's value to the end of the file. 
        /// Append this when rebuilding the file string after making changes to a tag.
        /// </summary>
        /// <param name="file"></param>
        /// <param name="tag"></param>
        /// <returns></returns>
        public string GetSuffix(ref string file, ref string tag)
        {
            int lineStart = GetTagStartPos(ref file, ref tag);
            int lineEnd = GetTagValueEndPos(ref file, lineStart);
            return file.Substring(lineEnd, file.Length - lineEnd);
        }


        /// <summary>
        ///     
        /// </summary>
        /// <param name="file"></param>
        /// <param name="tag"></param>
        /// <returns></returns>
        /// <remarks>            
        /// a tag has multiple values if no value on same line
        /// but one or more indented lines with "  - " that follow it
        /// ---OR---
        /// a bracket enclosed, comma-separated list on same line.
        /// Early out -- most are single line, so if
        /// (a) there is a possibly empty string val, not bracketed, on same line after colon
        /// --AND-- next line is either "---" or a new tag
        /// </remarks>
        public bool IsMultilineValue(ref string file, ref string tag)
        {
            string temp = tag + ":";
            int start = file.IndexOf(temp);
            int end = file.IndexOf("\n", start);
            var line = file.Substring(start, end - start);

            if (!line.Contains(":"))
            {
                Console.WriteLine("expected a : in metadata line");
                throw new Exception(); // TODO decide on error policy
            }

            // yes, then sanity check: is the next line a new tag?
            string nextLine = file.Substring(end + 1, file.IndexOf("\n", end + 1));
            if (Regex.IsMatch(nextLine, @"^[A_Za-z0-9\._-]+:"))
            {
                // tag is a single value 
                return false;
            }

            return true;
        }

        public bool IsMultilineValue(ref string file, int start)
        {

            int end = file.IndexOf("\n", start);
            var line = file.Substring(start, end - start);

            if (!line.Contains(":"))
            {
                Console.WriteLine("expected a : in metadata line");
                throw new Exception(); // TODO decide on error policy
            }

            // yes, then sanity check: is the next line a new tag?
            string nextLine = file.Substring(end + 1, file.IndexOf("\n", end + 1));
            if (Regex.IsMatch(nextLine, @"^[A_Za-z0-9\._-]+:"))
            {
                // tag is a single value 
                return false;
            }

            return true;
        }
        public static Dictionary<string, string> ParseYML(string yml)
        {
            var d = new Dictionary<string, string>();
            var lines = yml.Split('\n');

            // Theoretically matches only keys, not values. Needs good tests.
            Regex rgx = new Regex(@"[A-Za-z\._]+:");

            // Store current key for cases where we need to iterate over multiline values.
            // POssibly not needed.
            string currentKey = "";

            // For use in multiline values. All multiline values get enclosed in brackets, even if there is only
            // one value present.
            StringBuilder currentVal = new StringBuilder("{");

            foreach (var v in lines)
            {
                if (rgx.IsMatch(v)) // Are we on a new key, or a new value in a multiline value list?
                {
                    // we are on a new key, but have we just finshed appending a bunch of multiline vals
                    // that now need to be associated with the previous key in the dictionary?
                    if (currentVal.Length > 1)
                    {
                        currentVal.Append("}");
                        // currentKey is what we stored when we started a multiline parse.
                        // now we're ready to update the value
                        d[currentKey] = currentVal.ToString().Replace("\"- ", "\", ").Replace("{-", "{");

                        // reset the stringbuilder
                        currentVal.Clear();
                        currentVal.Append("{");
                    }

                    // We are on a key, so split into key - value at the colon
                    var pair = v.Split(':');
                    string str;
                    bool b = d.TryGetValue(pair[0], out str);
                    if (!b)
                    {
                        // add KV pair to dicctionary removing trailing or leading whitespace
                        d.Add(pair[0].Trim(), pair[1].Trim());
                        currentKey = pair[0].Trim(); // store in case we are about to parse a multiline value
                    }
                }
                else
                {
                    // we are on a multiline value, not a key
                    int beg = v.IndexOf(" - ");
                    // hacky sanity check, not very robust

                    if (beg >= 0 && beg < 5)
                    {
                        // add this into the string that we are building up
                        // for currentKey
                        currentVal.Append(v.Substring(beg).Trim());
                    }
                }
            }
            // we are  done looping, but if we have a string stored in currentVal
            // we need to add it to the dictionary. This happens when last key has multiline vals.
            if (currentVal.Length > 1)
            {
                currentVal.Append("}");
                d[currentKey] = currentVal.ToString().Trim().Replace("\"- ", "\", ").Replace("{-", "{");
            }
            return d;
        }
        #endregion

        #region CRUD operations

        public string DeleteTagAndValue(ref string file, ref string tag)
        {
            var pre = GetPrefix(ref file, ref tag);
            var suf = GetSuffix(ref file, ref tag);
            StringBuilder sb = new StringBuilder(pre);
            sb.Append(suf);
            return sb.ToString();
        }

        public string ReplaceSingleValue(ref string file, ref string tag, string newVal)
        {
            var pre = GetPrefix(ref file, ref tag);
            var suf = GetSuffix(ref file, ref tag);
            var old = GetTagAndValue(ref file, ref tag);
            var parts = old.Split(':');
            StringBuilder sb = new StringBuilder(pre);
            sb.Append(parts[0]).Append(": ").Append(newVal).Append("\r\n");
            return sb.ToString();
        }
        #endregion
    }
}

