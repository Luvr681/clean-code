﻿using System.Text;

namespace Markdown;

public class Md
{
    private StringBuilder _html;
    private ReplaceTagInspector _inspector;
    private TagConfiguration _currentTagConfiguration;
    private List<TagConfiguration> _tagConfigurations;
    private List<TagConfiguration> _tagConfigurationsNeedClosure;
    private string _currentSymbol;

    public Md()
    {
        _inspector = new ReplaceTagInspector();
        _tagConfigurations = new TagConfigurationList().TagConfigurations;
        _tagConfigurationsNeedClosure = new List<TagConfiguration>();
        //_titleCloseTagsIndexes = new List<int>();
    }
    
    public string Render(string markdown)
    {
        _html = new StringBuilder(markdown);
        _html.Append("  ");

        for (var i = 0; i < _html.Length - 1; i++)
        {
            _currentSymbol = DefineCurrentReplaceSymbol(i);
            
            if (HasTagWithSymbol(_currentSymbol))
            {
                _currentTagConfiguration = FindTagBySymbol(_currentSymbol);
                var isTitle = IsTitle(i);

                if (!isTitle)
                {
                    if (IsScreen(i))
                    {
                        var countOfScreens = PrepareScreensSymbols(i);
                        if (countOfScreens % 2 != 0)
                            continue;
                            
                        i -= countOfScreens;
                    }
                    
                    if (!CanReplaceTag(i))
                    {
                        i += _currentSymbol.Length;
                        continue;
                    }
                }

                var tagToReplace = DefineReplaceTag();
                    
                ReplaceSymbol(i, tagToReplace);
                if (isTitle)
                    PutTitleCloseTag(i);
            }
        }

        return _html.ToString().TrimEnd();
    }

    private void ReplaceSymbol(int index, string replaceTag)
    {
        _html.Remove(index, _currentTagConfiguration.Symbol.Length); 
        _html.Insert(index, replaceTag);
        ChangeTagConfigurationClosureFlag(); 
    } 
    
    private string DefineCurrentReplaceSymbol(int index) 
    { 
        var currentSymbol = "" + _html[index];
        if (HasEqualNeighbors(index) && !NeedClosure(FindTagBySymbol("_")))
            currentSymbol += _html[index];
        return currentSymbol;
    }
    
    private bool CanReplaceTag(int index)
    {
        var customConditions = new List<Func<int, bool>>()
        {
            index => 
                NeedClosure(FindTagBySymbol("_")) 
                && !NeedClosure(FindTagBySymbol("__")) 
                    ? HasEqualNeighbors(index) : !HasCloseTag(index),
            index => !NeedClosure(FindTagBySymbol("__")) && !HasCloseTag(index),
            index => !NeedClosure(_currentTagConfiguration) && IsInDifferentWords(index),
            index => !NeedClosure(_currentTagConfiguration) && IsIntersectsSymbols(index),
        };
        
        var inspectorConfig = new ReplaceTagInspectorConfig(
            _html, 
            _currentTagConfiguration, 
            index, 
            customConditions,
            NeedClosure(_currentTagConfiguration)
            );
        
        return _inspector.CanReplaceTag(inspectorConfig);
    }

    private void PutTitleCloseTag(int index)
    {
        var endOfParagraph = index;
        while (endOfParagraph < _html.Length - 1)
        {
            if (_html[endOfParagraph].Equals('\n'))
                break;
            endOfParagraph++;
        }

        _html.Insert(endOfParagraph, " ");
        ReplaceSymbol(endOfParagraph, FindTagBySymbol("#").CloseTag);
        if (_html[endOfParagraph - 1].Equals(' '))
            _html.Remove(endOfParagraph - 1, 1);
    }

    private int PrepareScreensSymbols(int index)
    {
        var countOfScreens = 0;
        while (index - 1 >= 0 && _html[index - 1].Equals('\\'))
        {
            _html.Remove(index - 1, 1);
            index--;
            countOfScreens++;
        }
        
        return countOfScreens;
    }

    private bool HasEqualNeighbors(int index)
    {
        return _html[index + 1].Equals(_html[index]);
    }
    
    private int GetIndexOfSpaceAfterOpenTag(int openTagIndex)
    {
        for (var i = openTagIndex + _currentTagConfiguration.Symbol.Length; i < _html.Length - 1; i++)
            if (_html[i].Equals(' '))
                return i;

        return -1;
    }

    private bool HasCloseTag(int index)
    {
        if (NeedClosure(FindTagBySymbol(_currentTagConfiguration.Symbol)))
            return true;

        var endOfSymbolIndex = index + _currentTagConfiguration.Symbol.Length;

        if (endOfSymbolIndex < _html.Length)
            return TryFindCloseTag(index) != -1;

        return false;
    }

    private int TryFindCloseTag(int index)
    {
        var closeTagIndex = IndexOfCloseTag(index); 
        if (closeTagIndex == -1)
            return -1;
        
        if (closeTagIndex > index && !IsCorrectCloseTagSymbol(closeTagIndex))
            TryFindCloseTag(closeTagIndex);

        return closeTagIndex;
    }

    private int IndexOfCloseTag(int index)
    {
        for (var i = index + _currentTagConfiguration.Symbol.Length; i < _html.Length - 1; i++)
        {
            var symbol = DefineCurrentReplaceSymbol(i);

            if (IsTriple(i))
                symbol = _currentTagConfiguration.Symbol;
            
            if (symbol.Equals(_currentTagConfiguration.Symbol))
                return GetEndOfPlentyIndex(i);

            if (HasTagWithSymbol(symbol))
                i += symbol.Length;
        }
        
        return -1;
    }

    private int GetEndOfPlentyIndex(int index)
    {
        if (_currentTagConfiguration.Symbol.Equals("_"))
            return index;
        if (index == -1)
            return -1;

        for (var i = index + 1; i < _html.Length - 1; i++)
        {
            if (!_html[i].Equals('_'))
                return i - _currentTagConfiguration.Symbol.Length;
        }

        return index;
    }

    private void ChangeTagConfigurationClosureFlag()
    {
        if (NeedClosure(_currentTagConfiguration))
            _tagConfigurationsNeedClosure.Remove(_currentTagConfiguration);
        else 
            _tagConfigurationsNeedClosure.Add(_currentTagConfiguration);
    }
    
    private string DefineReplaceTag()
    {
        return NeedClosure(_currentTagConfiguration)
            ? _currentTagConfiguration.CloseTag
            : _currentTagConfiguration.OpenTag;
    }
    
    private bool IsCorrectCloseTagSymbol(int closeTagIndex)
    {
        return !_html[closeTagIndex - 1].Equals(' '); 
    }

    private bool IsScreen(int index)
    {
        return index - 1 >= 0 && _html[index - 1].Equals('\\');
    }
    
    private bool IsTriple(int i)
    {
        var isTriple = i + 2 < _html.Length && _html[i] == _html[i + 1] && _html[i] == _html[i + 2];
        if (i + 3 < _html.Length && _html[i + 3] == _html[i] && isTriple)
            return false;

        return isTriple;
    }
    
    private bool IsIntersectsSymbols(int index)
    {
        var closeTagIdx = TryFindCloseTag(index);
        var countOfOtherTags = 0;
        
        for (var i = index + _currentTagConfiguration.Symbol.Length; i < closeTagIdx; i++)
        {
            if (_html[i].Equals('_'))
                countOfOtherTags++;
        }
        
        return countOfOtherTags % 2 != 0;
    }

    private bool IsInDifferentWords(int index)
    {
        var closeTagIndex = TryFindCloseTag(index);
        var nextSymbol = _html[closeTagIndex + 1];
        
        if (index == 0 && !Char.IsDigit(nextSymbol) && !Char.IsLetter(nextSymbol)) return false;
            
        return GetIndexOfSpaceAfterOpenTag(index) != -1 && GetIndexOfSpaceAfterOpenTag(index) < TryFindCloseTag(index);
    }
    
    private bool IsTitle(int index)
    {
        var isBeginingOfParagraph = index == 0 || _html[index - 1].Equals('\n');
        return isBeginingOfParagraph && _html[index].Equals('#');
    }
    
    private bool NeedClosure(TagConfiguration tagConfiguration)
    {
        return _tagConfigurationsNeedClosure.Contains(tagConfiguration);
    }

    private bool HasTagWithSymbol(string symbol)
    {
        return _tagConfigurations.Any(tag => tag.Symbol.Equals(symbol));
    }
    
    private TagConfiguration FindTagBySymbol(string symbol)
    {
        return _tagConfigurations.Where(tag => tag.Symbol.Equals(symbol)).First();
    }
}