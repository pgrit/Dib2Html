# Dib2Html

CLI tool to run dotnet interactive notebooks and store their outputs in a .HTML file.

Basically the same as converting a .dib to .ipynb, running it with Jupyter, saving it, and then using nbconvert to generate a .HTML with the command line options set to not include the cell inputs.

## Usage

```
dib2html notebook.dib
```

For more info and options, see
```
dib2html -h
```

## Install

```
dotnet tool install -g dib2html
```