TARGET = test
INCLUDE = ../Lib/Ec

$(TARGET): test.o 
	g++ $^ -o $(TARGET) -O3 -lpthread

test.o: test.cc
	g++ -c $^ -o $@ -std=c++14 -I$(INCLUDE) -O3

clean: 
	rm -f *.o $(TARGET)
