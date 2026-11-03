# Third-party notices

## Onomaverse names datasets - player names

The first names and surnames the game gives to generated players come from the **Onomaverse Names Datasets**
(release v2026.10: *Global Given-Name Frequency*, *Global Surname Frequency* and *Given-Name Gender Inference*).

> Names data from Onomaverse (https://onomaverse.com/datasets), licensed CC BY 4.0.

- Source: <https://onomaverse.com/datasets> - files at <https://github.com/onomaverse/datasets>
- Citation: The Onomaverse Team. *Onomaverse Names Datasets* (v2026.10). <https://onomaverse.com/datasets>. Licensed CC BY 4.0.
- Licence: Creative Commons Attribution 4.0 International (CC BY 4.0), <https://creativecommons.org/licenses/by/4.0/>
- **Changes made to the data:** only male given names (a name recorded for both sexes counts with the share of its bearers that are
  male, and not at all below 35%) and, for each of 32 countries, the 300 most common first names and up to 500 most common surnames
  were kept; names not written in the Latin script were left out; frequencies were turned into integer weights. The two lists of a
  country are kept apart. Names are otherwise copied as the dataset has them. See `tools/names/build_onomaverse_names.py`.
- **Not Onomaverse data:** the Croatian surnames. The release holds only 29 "Croatian" surnames and nearly all of them are not
  Croatian, so the game uses a short list of the most common Croatian surnames of its own (weights 1/rank). The Croatian first names
  are Onomaverse's.
- The generated data is `unity/SoccerVision/Assets/SoccerVision/Simulation/Names/OnomaverseNameData.cs`; the notice above is also in
  the code as `NameAttribution`. Wherever the names are shown to players (credits, the web page), show the notice.
