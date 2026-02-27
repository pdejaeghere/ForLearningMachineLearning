import numpy as np

classeur= {
"positif":[],
"negatif":[]
}
def trier(classeur,nombre):
    if nombre<0:
        classeur["negatif"].append(nombre);
    else:
        classeur["positif"].append(nombre);

    return classeur


def fibonaci(n):
    a=0
    b=1
    
    toreturn= [a]
    while(b<n):        
        a,b=b,a+b            
        toreturn.append(a);
    return toreturn;
    


trier(classeur,10);
trier(classeur,-1);
trier(classeur,-6);

print(classeur)

for k,v in classeur.items():
    print("key:",k,"value:",v)


malist=["1","2","3"]
dico={}
dico=dico.fromkeys(malist,'v')
print(dico)
dico.pop("2")
print(dico)
dico["1"]="toto"
print(dico)


print(fibonaci(10))